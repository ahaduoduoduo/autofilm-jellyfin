using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Rewrites existing Jellyfin paths to the OpenList path scheme in place.
/// </summary>
public interface IAutoFilmMigrationService
{
    /// <summary>
    /// Runs a bounded local-only migration preview or applies path rewrites.
    /// </summary>
    /// <param name="apply">Whether path rewrites should be persisted.</param>
    /// <param name="limit">Maximum number of candidate items.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The migration result.</returns>
    Task<AutoFilmMigrationResult> RunAsync(
        bool apply,
        int limit,
        CancellationToken cancellationToken);
}
