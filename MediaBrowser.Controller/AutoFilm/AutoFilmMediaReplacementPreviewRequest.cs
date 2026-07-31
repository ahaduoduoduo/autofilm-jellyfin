using System;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Requests a probe against one exact file and item.
/// </summary>
public sealed record AutoFilmMediaReplacementPreviewRequest
{
    /// <summary>Gets the target Jellyfin movie or episode ID.</summary>
    public Guid ItemId { get; init; }

    /// <summary>Gets the exact new OpenList file path.</summary>
    public string NewPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets an optional verified OpenList path for a legacy item whose stored
    /// path differs only by filename separators.
    /// </summary>
    public string? ResolvedOriginalPath { get; init; }
}
