using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Creates or refreshes Jellyfin items from a bounded OpenList snapshot.
/// </summary>
public interface IAutoFilmRemoteRefreshService
{
    /// <summary>
    /// Refreshes one remote path.
    /// </summary>
    /// <param name="request">Path-based request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved Jellyfin item.</returns>
    Task<AutoFilmRemoteRefreshResult> RefreshAsync(
        AutoFilmRemoteRefreshRequest request,
        CancellationToken cancellationToken);
}
