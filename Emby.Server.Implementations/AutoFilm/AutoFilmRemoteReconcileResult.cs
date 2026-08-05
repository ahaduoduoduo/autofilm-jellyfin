using MediaBrowser.Controller.Entities;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Result of one database reconciliation.
/// </summary>
/// <param name="Item">Current Jellyfin target.</param>
/// <param name="RemovedItems">Removed stale database items.</param>
/// <param name="ReclassifiedItems">Items recreated with the correct Jellyfin type.</param>
internal sealed record AutoFilmRemoteReconcileResult(
    BaseItem Item,
    int RemovedItems,
    int ReclassifiedItems)
{
    /// <summary>
    /// Gets an empty reconciliation result.
    /// </summary>
    /// <param name="item">Current Jellyfin target.</param>
    /// <returns>An empty reconciliation result for the target.</returns>
    public static AutoFilmRemoteReconcileResult Empty(BaseItem item) => new(item, 0, 0);
}
