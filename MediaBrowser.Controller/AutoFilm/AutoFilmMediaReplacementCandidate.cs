using System;

namespace MediaBrowser.Controller.AutoFilm;

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
