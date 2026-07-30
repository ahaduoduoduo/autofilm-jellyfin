using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Resolves remote and legacy external subtitles.
/// </summary>
public interface IAutoFilmSubtitleService
{
    /// <summary>
    /// Resolves one external subtitle using remote-first, local-fallback rules.
    /// </summary>
    /// <param name="itemId">Jellyfin item identifier.</param>
    /// <param name="streamIndex">Subtitle stream index.</param>
    /// <param name="requestedFormat">Subtitle format requested by the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolution, or <see langword="null"/> when not managed.</returns>
    Task<AutoFilmSubtitleResolution?> ResolveAsync(
        Guid itemId,
        int streamIndex,
        string requestedFormat,
        CancellationToken cancellationToken);

    /// <summary>
    /// Uploads a new subtitle directly to OpenList for a mapped video.
    /// </summary>
    /// <param name="itemId">Jellyfin item identifier.</param>
    /// <param name="format">Subtitle format.</param>
    /// <param name="language">Subtitle language.</param>
    /// <param name="isForced">Whether the subtitle is forced.</param>
    /// <param name="isHearingImpaired">Whether the subtitle is hearing impaired.</param>
    /// <param name="content">Decoded subtitle content stream.</param>
    /// <param name="contentLength">Known decoded content length, or null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The remote resolution, or <see langword="null"/> when the video is not mapped.</returns>
    Task<AutoFilmSubtitleResolution?> UploadAsync(
        Guid itemId,
        string format,
        string language,
        bool isForced,
        bool isHearingImpaired,
        Stream content,
        long? contentLength,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an external subtitle from OpenList and removes its stream.
    /// </summary>
    /// <param name="itemId">Jellyfin item identifier.</param>
    /// <param name="streamIndex">Subtitle stream index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the subtitle was managed by AutoFilm.</returns>
    Task<bool> DeleteAsync(
        Guid itemId,
        int streamIndex,
        CancellationToken cancellationToken);
}
