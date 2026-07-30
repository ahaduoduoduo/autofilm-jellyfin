using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Resolves a bounded OpenList snapshot with Jellyfin's normal item resolvers.
/// </summary>
public sealed class AutoFilmRemoteRefreshService : IAutoFilmRemoteRefreshService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly IAutoFilmOpenListClient _openListClient;
    private readonly IAutoFilmRemoteProbeQueue _probeQueue;
    private readonly IAutoFilmRemoteLibraryRoots _remoteLibraryRoots;
    private readonly AutoFilmOptions _options;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AutoFilmRemoteRefreshService"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="providerManager">Jellyfin metadata provider manager.</param>
    /// <param name="openListClient">OpenList path API.</param>
    /// <param name="probeQueue">Serialized remote ffprobe queue.</param>
    /// <param name="remoteLibraryRoots">Configured OpenList library roots.</param>
    /// <param name="options">AutoFilm configuration.</param>
    public AutoFilmRemoteRefreshService(
        ILibraryManager libraryManager,
        IProviderManager providerManager,
        IAutoFilmOpenListClient openListClient,
        IAutoFilmRemoteProbeQueue probeQueue,
        IAutoFilmRemoteLibraryRoots remoteLibraryRoots,
        AutoFilmOptions options)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _openListClient = openListClient;
        _probeQueue = probeQueue;
        _remoteLibraryRoots = remoteLibraryRoots;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<AutoFilmRemoteRefreshResult> RefreshAsync(
        AutoFilmRemoteRefreshRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var openListPath = NormalizeRequestPath(request.Path);
        var libraryRoot = _remoteLibraryRoots.FindRoot(openListPath)
            ?? throw new ArgumentException(
                "The remote path is outside the configured OpenList library roots.",
                nameof(request));
        var remotePath = AutoFilmRemotePath.FromOpenListPath(openListPath);
        var existing = _libraryManager.FindByPath(remotePath, null);
        var parent = FindNearestParent(openListPath, libraryRoot);
        if (existing is null && parent is null)
        {
            throw new ArgumentException(
                "The remote path does not belong to an existing Jellyfin library.",
                nameof(request));
        }

        var firstPath = existing is null
            ? GetOpenListPath(parent!.Path)
            : string.Equals(
                openListPath,
                libraryRoot,
                StringComparison.Ordinal)
                ? openListPath
                : parent is null
                    ? GetParentPath(openListPath)
                    : GetOpenListPath(parent.Path);
        var load = await LoadSnapshotAsync(
            firstPath,
            openListPath,
            request.Refresh,
            request.Recursive,
            cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            var importedItems = existing is Folder existingFolder
                ? ImportMissingDescendants(
                    existingFolder,
                    load.Snapshot,
                    request.Recursive,
                    cancellationToken)
                : Array.Empty<BaseItem>();
            var destinationParent = parent is null
                ? null
                : EnsureParentHierarchy(
                    parent,
                    GetParentPath(openListPath),
                    load.Snapshot);
            if (destinationParent is not null
                && !existing.ParentId.Equals(destinationParent.Id))
            {
                existing.SetParent(destinationParent);
                await existing.UpdateToRepositoryAsync(
                    ItemUpdateType.MetadataEdit,
                    cancellationToken).ConfigureAwait(false);
            }

            var providerTarget = ResolveProviderTarget(
                existing,
                request,
                load.Snapshot);
            ApplyProviderIds(providerTarget, request.ProviderIds);
            if (request.ProviderIds is { Count: > 0 })
            {
                await providerTarget.UpdateToRepositoryAsync(
                    ItemUpdateType.MetadataEdit,
                    cancellationToken).ConfigureAwait(false);
            }

            QueueMetadataRefresh(providerTarget, load.Snapshot);
            foreach (var importedVideo in importedItems
                         .OfType<Video>()
                         .Where(video => !video.Id.Equals(providerTarget.Id)))
            {
                QueueProbe(importedVideo, false);
            }

            QueueProbe(providerTarget, request.ForceProbe);
            return CreateResult(
                "refreshed",
                providerTarget,
                load,
                remotePath);
        }

        var currentParent = parent!;
        BaseItem? resolvedItem = null;
        foreach (var path in GetDescendantPaths(
                     GetOpenListPath(currentParent.Path),
                     openListPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pathUri = AutoFilmRemotePath.FromOpenListPath(path);
            var fileInfo = load.Snapshot.GetFileSystemEntry(pathUri)
                ?? throw new InvalidOperationException(
                    $"OpenList snapshot does not contain '{path}'.");
            var existingSegment = _libraryManager.FindByPath(
                pathUri,
                fileInfo.IsDirectory);
            if (existingSegment is not null)
            {
                resolvedItem = existingSegment;
            }
            else
            {
                resolvedItem = _libraryManager.ResolvePath(
                    fileInfo,
                    currentParent,
                    load.Snapshot);
                if (resolvedItem is null)
                {
                    throw new InvalidOperationException(
                        $"Jellyfin could not resolve remote path '{path}'.");
                }

                _libraryManager.CreateItem(resolvedItem, currentParent);
            }

            if (string.Equals(path, openListPath, StringComparison.Ordinal)
                || resolvedItem is not Folder resolvedFolder)
            {
                break;
            }

            currentParent = resolvedFolder;
        }

        if (resolvedItem is null)
        {
            throw new InvalidOperationException(
                $"Jellyfin did not create an item for '{openListPath}'.");
        }

        resolvedItem = ResolveProviderTarget(
            resolvedItem,
            request,
            load.Snapshot);
        ApplyProviderIds(resolvedItem, request.ProviderIds);
        if (request.ProviderIds is { Count: > 0 })
        {
            await resolvedItem.UpdateToRepositoryAsync(
                ItemUpdateType.MetadataEdit,
                cancellationToken).ConfigureAwait(false);
        }

        QueueMetadataRefresh(resolvedItem, load.Snapshot);
        QueueProbe(resolvedItem, request.ForceProbe);
        return CreateResult("created", resolvedItem, load, remotePath);
    }

    private Folder EnsureParentHierarchy(
        Folder nearestParent,
        string targetParentPath,
        AutoFilmDirectorySnapshot snapshot)
    {
        var currentParent = nearestParent;
        foreach (var path in GetDescendantPaths(
                     GetOpenListPath(nearestParent.Path),
                     targetParentPath))
        {
            var pathUri = AutoFilmRemotePath.FromOpenListPath(path);
            var fileInfo = snapshot.GetFileSystemEntry(pathUri)
                ?? throw new InvalidOperationException(
                    $"OpenList snapshot does not contain parent '{path}'.");
            if (!fileInfo.IsDirectory)
            {
                throw new InvalidOperationException(
                    $"Remote parent '{path}' is not a directory.");
            }

            var resolved = _libraryManager.FindByPath(pathUri, true);
            if (resolved is null)
            {
                resolved = _libraryManager.ResolvePath(
                    fileInfo,
                    currentParent,
                    snapshot)
                    ?? throw new InvalidOperationException(
                        $"Jellyfin could not resolve remote parent '{path}'.");
                _libraryManager.CreateItem(resolved, currentParent);
            }

            currentParent = resolved as Folder
                ?? throw new InvalidOperationException(
                    $"Remote parent '{path}' did not resolve as a folder.");
        }

        return currentParent;
    }

    private IReadOnlyList<BaseItem> ImportMissingDescendants(
        Folder root,
        AutoFilmDirectorySnapshot snapshot,
        bool recursive,
        CancellationToken cancellationToken)
    {
        if (!recursive)
        {
            return Array.Empty<BaseItem>();
        }

        var created = new List<BaseItem>();
        var pending = new Queue<Folder>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Enqueue(root);
        while (pending.TryDequeue(out var currentParent))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(currentParent.Path)
                || !visited.Add(currentParent.Path))
            {
                continue;
            }

            foreach (var entry in snapshot.GetFileSystemEntries(
                         currentParent.Path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = _libraryManager.FindByPath(
                    entry.FullName,
                    entry.IsDirectory);
                if (item is null)
                {
                    item = _libraryManager.ResolvePath(
                        entry,
                        currentParent,
                        snapshot);
                    if (item is null)
                    {
                        continue;
                    }

                    _libraryManager.CreateItem(item, currentParent);
                    created.Add(item);
                }

                if (item is Folder childFolder)
                {
                    pending.Enqueue(childFolder);
                }
            }
        }

        return created;
    }

    private async Task<SnapshotLoadResult> LoadSnapshotAsync(
        string existingParentPath,
        string targetPath,
        bool refresh,
        bool recursive,
        CancellationToken cancellationToken)
    {
        var snapshot = new AutoFilmDirectorySnapshot();
        var directoriesRead = 0;
        var objectsRead = 0;
        var listed = new HashSet<string>(StringComparer.Ordinal);
        var descendantPaths = GetDescendantPaths(
                existingParentPath,
                targetPath)
            .ToArray();

        if (descendantPaths.Length == 0)
        {
            var targetObject = await _openListClient.GetObjectAsync(
                targetPath,
                refresh,
                cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"OpenList path '{targetPath}' does not exist.");
            snapshot.Add(targetObject);
            objectsRead++;
            if (targetObject.IsDirectory)
            {
                var count = await LoadDirectoryAsync(
                    targetPath,
                    refresh,
                    snapshot,
                    listed,
                    cancellationToken).ConfigureAwait(false);
                directoriesRead++;
                objectsRead += count;
                EnforceDirectoryLimit(directoriesRead);
                EnforceObjectLimit(objectsRead);
            }
        }

        foreach (var path in descendantPaths)
        {
            var obj = await _openListClient.GetObjectAsync(
                path,
                refresh && string.Equals(
                    path,
                    targetPath,
                    StringComparison.Ordinal),
                cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"OpenList path '{path}' does not exist.");
            snapshot.Add(obj);
            objectsRead++;
            EnforceObjectLimit(objectsRead);

            if (obj.IsDirectory)
            {
                var count = await LoadDirectoryAsync(
                    path,
                    refresh,
                    snapshot,
                    listed,
                    cancellationToken).ConfigureAwait(false);
                directoriesRead++;
                objectsRead += count;
                EnforceDirectoryLimit(directoriesRead);
                EnforceObjectLimit(objectsRead);
            }
        }

        var target = snapshot.GetFileSystemEntry(
            AutoFilmRemotePath.FromOpenListPath(targetPath));
        if (recursive && target?.IsDirectory == true)
        {
            var queue = new Queue<string>();
            queue.Enqueue(targetPath);
            while (queue.TryDequeue(out var directory))
            {
                var before = snapshot.GetFileSystemEntries(
                    AutoFilmRemotePath.FromOpenListPath(directory));
                if (!listed.Contains(directory))
                {
                    var count = await LoadDirectoryAsync(
                        directory,
                        refresh,
                        snapshot,
                        listed,
                        cancellationToken).ConfigureAwait(false);
                    directoriesRead++;
                    objectsRead += count;
                    EnforceDirectoryLimit(directoriesRead);
                    EnforceObjectLimit(objectsRead);
                    before = snapshot.GetFileSystemEntries(
                        AutoFilmRemotePath.FromOpenListPath(directory));
                }

                foreach (var child in before.Where(entry => entry.IsDirectory))
                {
                    if (AutoFilmRemotePath.TryGetOpenListPath(
                            child.FullName,
                            out var childPath))
                    {
                        queue.Enqueue(childPath);
                    }
                }
            }
        }

        return new SnapshotLoadResult(
            snapshot,
            directoriesRead,
            objectsRead);
    }

    private async Task<int> LoadDirectoryAsync(
        string path,
        bool refresh,
        AutoFilmDirectorySnapshot snapshot,
        HashSet<string> listed,
        CancellationToken cancellationToken)
    {
        if (!listed.Add(path))
        {
            return 0;
        }

        var objects = await _openListClient.ListObjectsAsync(
            path,
            refresh,
            cancellationToken).ConfigureAwait(false);
        foreach (var obj in objects)
        {
            snapshot.Add(obj);
        }

        return objects.Count;
    }

    private Folder? FindNearestParent(string targetPath, string libraryRoot)
    {
        var candidate = GetParentPath(targetPath);
        while (AutoFilmRemotePath.IsWithinOpenListRoot(
                   candidate,
                   libraryRoot))
        {
            var remote = AutoFilmRemotePath.FromOpenListPath(candidate);
            if (_libraryManager.FindByPath(remote, true) is Folder remoteFolder)
            {
                return remoteFolder;
            }

            if (candidate == libraryRoot)
            {
                return null;
            }

            candidate = GetParentPath(candidate);
        }

        return null;
    }

    private string NormalizeRequestPath(string path)
    {
        if (AutoFilmRemotePath.TryGetOpenListPath(path, out var openListPath))
        {
            return openListPath;
        }

        return AutoFilmRemotePath.TryGetOpenListPath(
            AutoFilmRemotePath.FromOpenListPath(path),
            out openListPath)
            ? openListPath
            : throw new ArgumentException(
                "Path must be an OpenList absolute path or OpenList URI.",
                nameof(path));
    }

    private string GetOpenListPath(string jellyfinPath)
    {
        if (AutoFilmRemotePath.TryGetOpenListPath(
                jellyfinPath,
                out var openListPath))
        {
            return openListPath;
        }

        throw new InvalidOperationException(
            $"Parent path '{jellyfinPath}' is not an OpenList path.");
    }

    private void EnforceDirectoryLimit(int count)
    {
        if (count > _options.RemoteRefreshMaxDirectories)
        {
            throw new InvalidOperationException(
                $"Remote refresh exceeded the directory limit of {_options.RemoteRefreshMaxDirectories}.");
        }
    }

    private void EnforceObjectLimit(int count)
    {
        if (count > _options.RemoteRefreshMaxObjects)
        {
            throw new InvalidOperationException(
                $"Remote refresh exceeded the object limit of {_options.RemoteRefreshMaxObjects}.");
        }
    }

    private void QueueMetadataRefresh(
        BaseItem item,
        AutoFilmDirectorySnapshot snapshot)
    {
        _providerManager.QueueRefresh(
            item.Id,
            new MetadataRefreshOptions(snapshot)
            {
                MetadataRefreshMode = MetadataRefreshMode.Default,
                ImageRefreshMode = MetadataRefreshMode.Default,
                ReplaceAllMetadata = false,
                ReplaceAllImages = false,
                EnableRemoteContentProbe = false
            },
            RefreshPriority.High);
    }

    private BaseItem ResolveProviderTarget(
        BaseItem resolvedItem,
        AutoFilmRemoteRefreshRequest request,
        AutoFilmDirectorySnapshot snapshot)
    {
        if (!string.Equals(
                request.ProviderTarget,
                "movie",
                StringComparison.OrdinalIgnoreCase)
            || resolvedItem is not Folder folder)
        {
            return resolvedItem;
        }

        var candidates = snapshot
            .GetFileSystemEntries(folder.Path)
            .Where(entry => !entry.IsDirectory)
            .Select(entry =>
            {
                var existing = _libraryManager.FindByPath(
                    entry.FullName,
                    false);
                return (
                    Item: existing ?? _libraryManager.ResolvePath(
                        entry,
                        folder,
                        snapshot),
                    Exists: existing is not null);
            })
            .Where(candidate => candidate.Item is Video)
            .ToArray();
        if (candidates.Length != 1)
        {
            return resolvedItem;
        }

        var candidate = candidates[0];
        if (!candidate.Exists)
        {
            _libraryManager.CreateItem(candidate.Item!, folder);
        }

        return candidate.Item!;
    }

    private void QueueProbe(BaseItem item, bool force)
    {
        if (item is Video)
        {
            _probeQueue.Enqueue(item.Id, force);
        }
    }

    private static void ApplyProviderIds(
        BaseItem item,
        IReadOnlyDictionary<string, string>? providerIds)
    {
        if (providerIds is null)
        {
            return;
        }

        foreach (var pair in providerIds)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key)
                && !string.IsNullOrWhiteSpace(pair.Value))
            {
                item.SetProviderId(pair.Key, pair.Value);
            }
        }
    }

    private static IEnumerable<string> GetDescendantPaths(
        string parent,
        string target)
    {
        if (string.Equals(parent, target, StringComparison.Ordinal))
        {
            yield break;
        }

        var prefix = parent == "/"
            ? "/"
            : parent + "/";
        if (!target.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Target '{target}' is not below parent '{parent}'.");
        }

        var current = parent;
        foreach (var segment in target[prefix.Length..]
                     .Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current == "/"
                ? "/" + segment
                : current + "/" + segment;
            yield return current;
        }
    }

    private static string GetParentPath(string path)
    {
        if (path == "/")
        {
            return "/";
        }

        var separator = path.LastIndexOf('/');
        return separator <= 0
            ? "/"
            : path[..separator];
    }

    private static AutoFilmRemoteRefreshResult CreateResult(
        string action,
        BaseItem item,
        SnapshotLoadResult load,
        string requestedPath)
    {
        return new AutoFilmRemoteRefreshResult(
            action,
            item.Id,
            item.Name,
            item.GetType().Name,
            requestedPath,
            load.DirectoriesRead,
            load.ObjectsRead);
    }

    private sealed record SnapshotLoadResult(
        AutoFilmDirectorySnapshot Snapshot,
        int DirectoriesRead,
        int ObjectsRead);
}
