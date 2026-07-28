using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Applies OpenList notifications without an object mapping catalog.
/// </summary>
public sealed class AutoFilmPathEventService : IAutoFilmPathEventService
{
    private static readonly string[] SubtitleExtensions =
        [".ass", ".idx", ".srt", ".ssa", ".sub", ".sup", ".vtt"];

    private readonly ILibraryManager _libraryManager;
    private readonly IMediaStreamRepository _mediaStreamRepository;
    private readonly IAutoFilmRemoteRefreshService _remoteRefreshService;
    private readonly IAutoFilmRemoteLibraryRoots _remoteLibraryRoots;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoFilmPathEventService"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="mediaStreamRepository">Jellyfin stream repository.</param>
    /// <param name="remoteRefreshService">Precise remote refresh service.</param>
    /// <param name="remoteLibraryRoots">Configured OpenList library roots.</param>
    public AutoFilmPathEventService(
        ILibraryManager libraryManager,
        IMediaStreamRepository mediaStreamRepository,
        IAutoFilmRemoteRefreshService remoteRefreshService,
        IAutoFilmRemoteLibraryRoots remoteLibraryRoots)
    {
        _libraryManager = libraryManager;
        _mediaStreamRepository = mediaStreamRepository;
        _remoteRefreshService = remoteRefreshService;
        _remoteLibraryRoots = remoteLibraryRoots;
    }

    /// <inheritdoc />
    public async Task<AutoFilmPathEventResult> ApplyAsync(
        AutoFilmPathEvent eventItem,
        CancellationToken cancellationToken)
    {
        Validate(eventItem);
        var currentIsInLibrary =
            _remoteLibraryRoots.FindRoot(eventItem.Path) is not null;
        if (eventItem.Type == "object.move")
        {
            var oldIsInLibrary = _remoteLibraryRoots.FindRoot(
                eventItem.OldPath!) is not null;
            if (!currentIsInLibrary && !oldIsInLibrary)
            {
                return Result(eventItem, "outside_library", 0);
            }

            if (!currentIsInLibrary)
            {
                return ApplyRemove(eventItem with
                {
                    Path = eventItem.OldPath!,
                    Type = "object.remove"
                });
            }

            if (!oldIsInLibrary)
            {
                return await ApplyUpsertAsync(
                    eventItem,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        else if (!currentIsInLibrary)
        {
            return Result(eventItem, "outside_library", 0);
        }

        return eventItem.Type switch
        {
            "object.upsert" => await ApplyUpsertAsync(
                eventItem,
                cancellationToken).ConfigureAwait(false),
            "object.move" => await ApplyMoveAsync(
                eventItem,
                cancellationToken).ConfigureAwait(false),
            "object.remove" => ApplyRemove(eventItem),
            _ => throw new ArgumentException(
                $"Unsupported AutoFilm event type '{eventItem.Type}'.",
                nameof(eventItem))
        };
    }

    private async Task<AutoFilmPathEventResult> ApplyUpsertAsync(
        AutoFilmPathEvent eventItem,
        CancellationToken cancellationToken)
    {
        if (IsSubtitle(eventItem.Path))
        {
            return Result(eventItem, "subtitle_deferred", 0);
        }

        var refresh = await _remoteRefreshService.RefreshAsync(
            new AutoFilmRemoteRefreshRequest
            {
                Path = eventItem.Path,
                Refresh = false,
                Recursive = eventItem.IsDirectory,
                ForceProbe = !eventItem.IsDirectory
            },
            cancellationToken).ConfigureAwait(false);
        return Result(eventItem, refresh.Action, 1);
    }

    private async Task<AutoFilmPathEventResult> ApplyMoveAsync(
        AutoFilmPathEvent eventItem,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventItem.OldPath))
        {
            throw new ArgumentException(
                "Move events require old_path.",
                nameof(eventItem));
        }

        var oldUri = AutoFilmRemotePath.FromOpenListPath(eventItem.OldPath);
        var newUri = AutoFilmRemotePath.FromOpenListPath(eventItem.Path);
        var root = _libraryManager.FindByPath(
            oldUri,
            eventItem.IsDirectory);
        if (root is null)
        {
            return await ApplyUpsertAsync(
                eventItem,
                cancellationToken).ConfigureAwait(false);
        }

        var items = new List<BaseItem> { root };
        if (root is Folder folder)
        {
            items.AddRange(folder.GetRecursiveChildren());
        }

        var changed = 0;
        foreach (var item in items.OrderBy(candidate => candidate.Path?.Length ?? 0))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReplacePrefix(item.Path, oldUri, newUri, out var updatedPath))
            {
                continue;
            }

            item.Path = updatedPath;
            await item.UpdateToRepositoryAsync(
                ItemUpdateType.MetadataEdit,
                cancellationToken).ConfigureAwait(false);
            RewriteSubtitlePaths(item.Id, oldUri, newUri, cancellationToken);
            changed++;
        }

        await _remoteRefreshService.RefreshAsync(
            new AutoFilmRemoteRefreshRequest
            {
                Path = eventItem.Path,
                Refresh = false,
                Recursive = false,
                ForceProbe = !eventItem.IsDirectory
            },
            cancellationToken).ConfigureAwait(false);
        return Result(eventItem, "moved", changed);
    }

    private AutoFilmPathEventResult ApplyRemove(AutoFilmPathEvent eventItem)
    {
        if (IsSubtitle(eventItem.Path))
        {
            return Result(eventItem, "subtitle_deferred", 0);
        }

        var remoteUri = AutoFilmRemotePath.FromOpenListPath(eventItem.Path);
        var item = _libraryManager.FindByPath(
            remoteUri,
            eventItem.IsDirectory);
        if (item is null)
        {
            return Result(eventItem, "already_absent", 0);
        }

        _libraryManager.DeleteItem(
            item,
            new DeleteOptions { DeleteFileLocation = false },
            true);
        return Result(eventItem, "removed", 1);
    }

    private void RewriteSubtitlePaths(
        Guid itemId,
        string oldUri,
        string newUri,
        CancellationToken cancellationToken)
    {
        var streams = _mediaStreamRepository.GetMediaStreams(
            new MediaStreamQuery { ItemId = itemId });
        var changed = false;
        foreach (var stream in streams)
        {
            if (stream.Type == MediaStreamType.Subtitle
                && stream.IsExternal
                && TryReplacePrefix(
                    stream.Path,
                    oldUri,
                    newUri,
                    out var updatedPath))
            {
                stream.Path = updatedPath;
                changed = true;
            }
        }

        if (changed)
        {
            _mediaStreamRepository.SaveMediaStreams(
                itemId,
                streams,
                cancellationToken);
        }
    }

    private static bool TryReplacePrefix(
        string? value,
        string oldPrefix,
        string newPrefix,
        out string updated)
    {
        updated = value ?? string.Empty;
        if (string.IsNullOrEmpty(value)
            || (!string.Equals(value, oldPrefix, StringComparison.Ordinal)
                && !value.StartsWith(oldPrefix + "/", StringComparison.Ordinal)))
        {
            return false;
        }

        updated = newPrefix + value[oldPrefix.Length..];
        return true;
    }

    private static bool IsSubtitle(string path)
    {
        return SubtitleExtensions.Contains(
            Path.GetExtension(path),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void Validate(AutoFilmPathEvent eventItem)
    {
        if (string.IsNullOrWhiteSpace(eventItem.EventId))
        {
            throw new ArgumentException(
                "event_id is required.",
                nameof(eventItem));
        }

        _ = AutoFilmRemotePath.FromOpenListPath(eventItem.Path);
        if (eventItem.Type == "object.move")
        {
            if (string.IsNullOrWhiteSpace(eventItem.OldPath))
            {
                throw new ArgumentException(
                    "Move events require old_path.",
                    nameof(eventItem));
            }

            _ = AutoFilmRemotePath.FromOpenListPath(eventItem.OldPath);
        }
    }

    private static AutoFilmPathEventResult Result(
        AutoFilmPathEvent eventItem,
        string action,
        int itemsChanged)
    {
        return new AutoFilmPathEventResult(
            eventItem.Sequence,
            eventItem.EventId,
            action,
            itemsChanged);
    }
}
