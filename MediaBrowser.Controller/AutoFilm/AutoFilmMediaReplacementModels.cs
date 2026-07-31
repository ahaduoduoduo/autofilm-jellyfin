using System;
using System.Collections.Generic;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Requests read-only video discovery below one OpenList path.
/// </summary>
public sealed record AutoFilmMediaReplacementInspectRequest
{
    /// <summary>Gets the OpenList absolute path or persistent URI.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether subdirectories are read.</summary>
    public bool Recursive { get; init; } = true;
}

/// <summary>
/// One video recognized with Jellyfin naming rules.
/// </summary>
public sealed record AutoFilmMediaReplacementCandidate(
    string Path,
    string Name,
    string? Container,
    long Size,
    DateTime Modified,
    string? ExtraType,
    int? SeasonNumber,
    int? EpisodeNumber,
    int? EndingEpisodeNumber);

/// <summary>
/// Read-only discovery result.
/// </summary>
public sealed record AutoFilmMediaReplacementInspectResult(
    string RequestedPath,
    int DirectoriesRead,
    int ObjectsRead,
    IReadOnlyList<AutoFilmMediaReplacementCandidate> Candidates);

/// <summary>
/// Requests a probe against one exact file and item.
/// </summary>
public sealed record AutoFilmMediaReplacementPreviewRequest
{
    /// <summary>Gets the target Jellyfin movie or episode ID.</summary>
    public Guid ItemId { get; init; }

    /// <summary>Gets the exact new OpenList file path.</summary>
    public string NewPath { get; init; } = string.Empty;
}

/// <summary>
/// Media facts used for replacement review and verification.
/// </summary>
public sealed record AutoFilmMediaReplacementFacts(
    string Path,
    long? Size,
    long? RunTimeTicks,
    int? Bitrate,
    string? Container,
    int? Width,
    int? Height,
    IReadOnlyList<MediaStream> Streams);

/// <summary>
/// Short-lived immutable replacement plan.
/// </summary>
public sealed record AutoFilmMediaReplacementPreview(
    string PreviewToken,
    DateTimeOffset ExpiresAt,
    Guid ItemId,
    string ItemName,
    string ItemType,
    AutoFilmMediaReplacementFacts Current,
    AutoFilmMediaReplacementFacts Replacement);

/// <summary>
/// Applies an immutable preview.
/// </summary>
public sealed record AutoFilmMediaReplacementApplyRequest
{
    /// <summary>Gets the preview token.</summary>
    public string PreviewToken { get; init; } = string.Empty;
}

/// <summary>
/// Restores one successful replacement.
/// </summary>
public sealed record AutoFilmMediaReplacementRollbackRequest
{
    /// <summary>Gets the rollback token returned by Apply.</summary>
    public string RollbackToken { get; init; } = string.Empty;
}

/// <summary>
/// Result of applying or restoring a replacement.
/// </summary>
public sealed record AutoFilmMediaReplacementResult(
    string State,
    Guid ItemId,
    string PreviousPath,
    string CurrentPath,
    string? RollbackToken,
    AutoFilmMediaReplacementFacts Current);
