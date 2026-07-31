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
    Task<AutoFilmMediaReplacementInspectResult> InspectAsync(
        AutoFilmMediaReplacementInspectRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Probes one exact replacement file and creates a short-lived plan.
    /// </summary>
    Task<AutoFilmMediaReplacementPreview> PreviewAsync(
        AutoFilmMediaReplacementPreviewRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies a previously probed plan to the same Jellyfin item ID.
    /// </summary>
    Task<AutoFilmMediaReplacementResult> ApplyAsync(
        AutoFilmMediaReplacementApplyRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Restores the item snapshot captured by a successful apply operation.
    /// </summary>
    Task<AutoFilmMediaReplacementResult> RollbackAsync(
        AutoFilmMediaReplacementRollbackRequest request,
        CancellationToken cancellationToken);
}
