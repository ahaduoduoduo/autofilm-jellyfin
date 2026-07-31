using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Inspects remote videos and replaces the media backing an existing item
/// without creating a second library record.
/// </summary>
public interface IAutoFilmMediaReplacementService
{
    /// <summary>
    /// Finds Jellyfin-recognized videos below one OpenList path without
    /// creating library items.
    /// </summary>
    /// <param name="request">The bounded OpenList discovery request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The videos recognized below the requested path.</returns>
    Task<AutoFilmMediaReplacementInspectResult> InspectAsync(
        AutoFilmMediaReplacementInspectRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Probes one exact replacement file and creates a short-lived plan.
    /// </summary>
    /// <param name="request">The exact item and replacement file request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The immutable replacement preview.</returns>
    Task<AutoFilmMediaReplacementPreview> PreviewAsync(
        AutoFilmMediaReplacementPreviewRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies a previously probed plan to the same Jellyfin item ID.
    /// </summary>
    /// <param name="request">The immutable preview token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated Jellyfin item state.</returns>
    Task<AutoFilmMediaReplacementResult> ApplyAsync(
        AutoFilmMediaReplacementApplyRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Restores the item snapshot captured by a successful apply operation.
    /// </summary>
    /// <param name="request">The rollback token returned by apply.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The restored Jellyfin item state.</returns>
    Task<AutoFilmMediaReplacementResult> RollbackAsync(
        AutoFilmMediaReplacementRollbackRequest request,
        CancellationToken cancellationToken);
}
