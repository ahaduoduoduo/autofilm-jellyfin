using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Applies path-only OpenList events idempotently.
/// </summary>
public interface IAutoFilmPathEventService
{
    /// <summary>
    /// Applies one ordered event.
    /// </summary>
    /// <param name="eventItem">OpenList event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The local action.</returns>
    Task<AutoFilmPathEventResult> ApplyAsync(
        AutoFilmPathEvent eventItem,
        CancellationToken cancellationToken);
}
