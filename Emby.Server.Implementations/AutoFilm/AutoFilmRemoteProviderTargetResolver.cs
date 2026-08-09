using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Selects and prepares the logical metadata target for an OpenList refresh.
/// </summary>
internal static class AutoFilmRemoteProviderTargetResolver
{
    /// <summary>
    /// Applies non-empty provider identifiers to the logical metadata target.
    /// </summary>
    /// <param name="item">Logical metadata target.</param>
    /// <param name="providerIds">Provider identifiers from the refresh request.</param>
    internal static void ApplyProviderIds(
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

    /// <summary>
    /// Resolves the item that owns provider identifiers for the request.
    /// </summary>
    /// <param name="resolvedItem">Physical item resolved from the request path.</param>
    /// <param name="request">Remote refresh request.</param>
    /// <param name="snapshot">Bounded OpenList snapshot.</param>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <returns>The logical metadata target.</returns>
    internal static BaseItem Resolve(
        BaseItem resolvedItem,
        AutoFilmRemoteRefreshRequest request,
        AutoFilmDirectorySnapshot snapshot,
        ILibraryManager libraryManager)
    {
        if (!string.Equals(
                request.ProviderTarget,
                "movie",
                StringComparison.OrdinalIgnoreCase))
        {
            return FindOwningSeries(resolvedItem, libraryManager)
                ?? resolvedItem;
        }

        if (resolvedItem is not Folder folder)
        {
            return resolvedItem;
        }

        var candidates = snapshot
            .GetFileSystemEntries(folder.Path)
            .Where(entry => !entry.IsDirectory)
            .Select(entry =>
            {
                var existing = libraryManager.FindByPath(
                    entry.FullName,
                    false);
                return (
                    Item: existing ?? libraryManager.ResolvePath(
                        entry,
                        folder,
                        snapshot,
                        libraryManager.GetContentType(folder)),
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
            libraryManager.CreateItem(candidate.Item!, folder);
        }

        return candidate.Item!;
    }

    /// <summary>
    /// Persists season and episode numbers before the owning Series is refreshed.
    /// </summary>
    /// <param name="videos">Videos discovered by the remote import.</param>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The asynchronous operation.</returns>
    internal static async Task PrepareEpisodesAsync(
        IReadOnlyList<Video> videos,
        ILibraryManager libraryManager,
        CancellationToken cancellationToken)
    {
        foreach (var episode in videos.OfType<Episode>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!libraryManager.FillMissingEpisodeNumbersFromPath(
                    episode,
                    false))
            {
                continue;
            }

            await episode.UpdateToRepositoryAsync(
                ItemUpdateType.MetadataImport,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Removes provider identifiers written to a Season by the earlier refresh behavior.
    /// </summary>
    /// <param name="resolvedItem">Physical item resolved from the request path.</param>
    /// <param name="providerTarget">Logical provider target.</param>
    /// <param name="providerIds">Provider identifiers moved to the Series.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The asynchronous operation.</returns>
    internal static async Task RemoveMisplacedSeriesProviderIdsAsync(
        BaseItem resolvedItem,
        BaseItem providerTarget,
        IReadOnlyDictionary<string, string> providerIds,
        CancellationToken cancellationToken)
    {
        if (providerTarget is not Series series)
        {
            return;
        }

        var seasons = series.GetRecursiveChildren(false)
            .OfType<Season>()
            .Concat(resolvedItem is Season resolvedSeason
                ? new[] { resolvedSeason }
                : Array.Empty<Season>())
            .DistinctBy(season => season.Id)
            .ToArray();
        foreach (var season in seasons)
        {
            var changed = false;
            foreach (var pair in providerIds)
            {
                if (season.ProviderIds.TryGetValue(pair.Key, out var value)
                    && string.Equals(value, pair.Value, StringComparison.Ordinal))
                {
                    season.RemoveProviderId(pair.Key);
                    changed = true;
                }
            }

            if (changed)
            {
                await season.UpdateToRepositoryAsync(
                    ItemUpdateType.MetadataEdit,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Returns physical seasons already present below the logical metadata target.
    /// </summary>
    /// <param name="providerTarget">Logical provider target.</param>
    /// <returns>Existing physical seasons.</returns>
    internal static IReadOnlyList<Season> GetExistingSeasons(
        BaseItem providerTarget)
    {
        return providerTarget is Series series
            ? series.GetRecursiveChildren(false).OfType<Season>().ToArray()
            : Array.Empty<Season>();
    }

    /// <summary>
    /// Queues provider metadata after the Series identity and episode numbers are saved.
    /// </summary>
    /// <param name="providerManager">Jellyfin provider manager.</param>
    /// <param name="providerTarget">Logical metadata target.</param>
    /// <param name="videos">Videos discovered by the remote import.</param>
    /// <param name="snapshot">Bounded OpenList snapshot.</param>
    /// <param name="forceProviderRefresh">Whether provider metadata must be fetched again.</param>
    internal static void QueueMetadataRefreshes(
        IProviderManager providerManager,
        BaseItem providerTarget,
        IReadOnlyList<Video> videos,
        AutoFilmDirectorySnapshot snapshot,
        bool forceProviderRefresh)
    {
        var refreshMode = forceProviderRefresh
            ? MetadataRefreshMode.FullRefresh
            : MetadataRefreshMode.Default;
        QueueMetadataRefresh(
            providerManager,
            providerTarget,
            snapshot,
            refreshMode,
            RefreshPriority.High,
            false);
        foreach (var season in GetExistingSeasons(providerTarget))
        {
            QueueMetadataRefresh(
                providerManager,
                season,
                snapshot,
                refreshMode,
                RefreshPriority.Normal,
                false);
        }

        foreach (var video in videos
                     .Where(video => !video.Id.Equals(providerTarget.Id)))
        {
            QueueMetadataRefresh(
                providerManager,
                video,
                snapshot,
                refreshMode,
                RefreshPriority.Normal,
                forceProviderRefresh);
        }
    }

    /// <summary>
    /// Finds the nearest series without relying on the physical directory depth.
    /// </summary>
    /// <param name="item">Resolved refresh target.</param>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <returns>The owning Series, or <see langword="null"/> outside a Series.</returns>
    internal static Series? FindOwningSeries(
        BaseItem item,
        ILibraryManager libraryManager)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(libraryManager);

        if (item is Series series)
        {
            return series;
        }

        var parentId = item.ParentId;
        var visited = new HashSet<Guid>();
        while (!parentId.Equals(Guid.Empty) && visited.Add(parentId))
        {
            var parent = libraryManager.GetItemById(parentId);
            if (parent is null)
            {
                return null;
            }

            if (parent is Series owningSeries)
            {
                return owningSeries;
            }

            parentId = parent.ParentId;
        }

        return null;
    }

    private static void QueueMetadataRefresh(
        IProviderManager providerManager,
        BaseItem item,
        AutoFilmDirectorySnapshot snapshot,
        MetadataRefreshMode refreshMode,
        RefreshPriority priority,
        bool replaceAllMetadata)
    {
        providerManager.QueueRefresh(
            item.Id,
            new MetadataRefreshOptions(snapshot)
            {
                MetadataRefreshMode = refreshMode,
                ImageRefreshMode = refreshMode,
                ReplaceAllMetadata = replaceAllMetadata,
                ReplaceAllImages = false,
                EnableRemoteContentProbe = false
            },
            priority);
    }
}
