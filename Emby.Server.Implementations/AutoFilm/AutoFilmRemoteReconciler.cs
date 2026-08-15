using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Reconciles one Jellyfin subtree with a fresh, bounded OpenList snapshot.
/// </summary>
public sealed class AutoFilmRemoteReconciler
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<AutoFilmRemoteReconciler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoFilmRemoteReconciler"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="logger">Logger.</param>
    public AutoFilmRemoteReconciler(
        ILibraryManager libraryManager,
        ILogger<AutoFilmRemoteReconciler> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>
    /// Removes stale database-only descendants and replaces incorrectly typed items.
    /// </summary>
    /// <param name="target">Existing Jellyfin target.</param>
    /// <param name="parent">Existing parent of the requested path.</param>
    /// <param name="snapshot">Fresh OpenList snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current target and reconciliation counts.</returns>
    internal async Task<AutoFilmRemoteReconcileResult> ReconcileAsync(
        BaseItem target,
        Folder? parent,
        AutoFilmDirectorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FileSystemMetadata> remoteEntries =
            Array.Empty<FileSystemMetadata>();
        BaseItem[] databaseEntries = Array.Empty<BaseItem>();
        if (target is Folder guardedFolder)
        {
            remoteEntries = snapshot.GetEntriesWithin(guardedFolder.Path);
            databaseEntries = guardedFolder.GetRecursiveChildren(false)
                .Where(item => AutoFilmRemotePath.IsRemote(item.Path))
                .ToArray();
            if (remoteEntries.Count == 0 && databaseEntries.Length > 0)
            {
                throw new InvalidOperationException(
                    "OpenList returned an empty directory while Jellyfin still has media below it; full rescan was refused.");
            }
        }

        var targetEntry = snapshot.GetFileSystemEntry(target.Path);
        if (targetEntry is not null && parent is not null)
        {
            var resolvedTarget = Resolve(
                targetEntry,
                parent,
                snapshot,
                target);
            if (resolvedTarget is not null && RequiresReplacement(target, resolvedTarget))
            {
                var replacement = await ReplaceAsync(
                    target,
                    resolvedTarget,
                    parent,
                    cancellationToken).ConfigureAwait(false);
                return new AutoFilmRemoteReconcileResult(replacement, 1, 1);
            }
        }

        if (target is not Folder folder)
        {
            return new AutoFilmRemoteReconcileResult(target, 0, 0);
        }

        var remotePaths = remoteEntries
            .Select(entry => entry.FullName)
            .ToHashSet(StringComparer.Ordinal);
        var stale = databaseEntries
            .Where(item => !remotePaths.Contains(item.Path))
            .ToArray();
        var stalePaths = stale
            .Select(item => item.Path)
            .ToHashSet(StringComparer.Ordinal);
        var staleRoots = stale
            .Where(item => !HasStaleAncestor(item.Path, stalePaths))
            .OrderByDescending(item => item.Path.Length)
            .ToArray();
        foreach (var item in staleRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation(
                "Removing stale OpenList database item {ItemId} at {Path}",
                item.Id,
                item.Path);
            _libraryManager.DeleteItem(
                item,
                new DeleteOptions { DeleteFileLocation = false },
                false);
        }

        var reclassified = 0;
        foreach (var entry in remoteEntries.OrderBy(entry => PathDepth(entry.FullName)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = _libraryManager.FindByPath(
                entry.FullName,
                entry.IsDirectory);
            if (current is null)
            {
                continue;
            }

            var currentParent = FindParent(entry.FullName, folder);
            if (currentParent is null)
            {
                continue;
            }

            var resolved = Resolve(
                entry,
                currentParent,
                snapshot,
                current);
            if (resolved is null || !RequiresReplacement(current, resolved))
            {
                continue;
            }

            await ReplaceAsync(
                current,
                resolved,
                currentParent,
                cancellationToken).ConfigureAwait(false);
            reclassified++;
        }

        return new AutoFilmRemoteReconcileResult(
            target,
            stale.Length,
            reclassified);
    }

    private BaseItem? Resolve(
        FileSystemMetadata entry,
        Folder parent,
        AutoFilmDirectorySnapshot snapshot,
        BaseItem current)
    {
        return AutoFilmRemoteMovieResolver.ResolveEntry(
            entry,
            parent,
            snapshot,
            _libraryManager,
            ShouldPreserveMovieIdentity(current));
    }

    internal static bool ShouldPreserveMovieIdentity(BaseItem item)
    {
        return item is Movie
            || (item is Video and not Episode
                && item.ProviderIds.Count > 0);
    }

    private async Task<BaseItem> ReplaceAsync(
        BaseItem current,
        BaseItem replacement,
        Folder parent,
        CancellationToken cancellationToken)
    {
        var metadataSource = replacement is Video && current is Folder folder
            ? folder.GetRecursiveChildren(false)
                .OfType<Video>()
                .FirstOrDefault(item => string.Equals(
                    item.Path,
                    replacement.Path,
                    StringComparison.Ordinal))
                ?? current
            : current;
        CopyMetadata(metadataSource, replacement);
        if (!ReferenceEquals(metadataSource, current))
        {
            CopyMissingProviderIds(current, replacement);
        }

        var oldIds = new[] { current.Id, metadataSource.Id }
            .Where(id => !id.Equals(replacement.Id))
            .Distinct()
            .ToArray();

        _logger.LogInformation(
            "Reclassifying OpenList item {Path} from {OldType} to {NewType}",
            current.Path,
            current.GetType().Name,
            replacement.GetType().Name);
        _libraryManager.DeleteItem(
            current,
            new DeleteOptions { DeleteFileLocation = false },
            false);
        replacement.SetParent(parent);
        var persisted = _libraryManager.GetItemById(replacement.Id)
            ?? _libraryManager.FindByPath(replacement.Path, replacement.IsFolder);
        if (persisted is null)
        {
            _libraryManager.CreateItem(replacement, parent);
            persisted = replacement;
        }

        foreach (var oldId in oldIds)
        {
            await _libraryManager.RerouteLinkedChildReferencesAsync(
                oldId,
                persisted.Id).ConfigureAwait(false);
        }

        await persisted.ReattachUserDataAsync(cancellationToken).ConfigureAwait(false);
        return persisted;
    }

    private Folder? FindParent(string remotePath, Folder root)
    {
        var parentPath = GetParentPath(remotePath);
        if (string.Equals(parentPath, root.Path, StringComparison.Ordinal))
        {
            return root;
        }

        return _libraryManager.FindByPath(parentPath, true) as Folder;
    }

    private static bool RequiresReplacement(BaseItem current, BaseItem resolved)
    {
        return current.GetType() != resolved.GetType()
            || !current.Id.Equals(resolved.Id)
            || !string.Equals(current.Path, resolved.Path, StringComparison.Ordinal);
    }

    private static bool HasStaleAncestor(
        string path,
        IReadOnlySet<string> stalePaths)
    {
        var parent = GetParentPath(path);
        while (!string.Equals(parent, path, StringComparison.Ordinal))
        {
            if (stalePaths.Contains(parent))
            {
                return true;
            }

            if (parent == "openlist:///")
            {
                break;
            }

            path = parent;
            parent = GetParentPath(path);
        }

        return false;
    }

    private static string GetParentPath(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator <= "openlist://".Length
            ? "openlist:///"
            : path[..separator];
    }

    private static int PathDepth(string path)
    {
        return path.Count(character => character == '/');
    }

    private static void CopyMetadata(BaseItem source, BaseItem destination)
    {
        destination.Name = source.Name;
        destination.OriginalTitle = source.OriginalTitle;
        destination.ForcedSortName = source.ForcedSortName;
        destination.Overview = source.Overview;
        destination.PremiereDate = source.PremiereDate;
        destination.EndDate = source.EndDate;
        destination.ProductionYear = source.ProductionYear;
        destination.CommunityRating = source.CommunityRating;
        destination.CriticRating = source.CriticRating;
        destination.OfficialRating = source.OfficialRating;
        destination.CustomRating = source.CustomRating;
        destination.ProviderIds = new Dictionary<string, string>(
            source.ProviderIds,
            StringComparer.OrdinalIgnoreCase);
        destination.Genres = source.Genres?.ToArray() ?? Array.Empty<string>();
        destination.Studios = source.Studios?.ToArray() ?? Array.Empty<string>();
        destination.Tags = source.Tags?.ToArray() ?? Array.Empty<string>();
    }

    private static void CopyMissingProviderIds(
        BaseItem source,
        BaseItem destination)
    {
        foreach (var providerId in source.ProviderIds)
        {
            destination.ProviderIds.TryAdd(providerId.Key, providerId.Value);
        }
    }
}
