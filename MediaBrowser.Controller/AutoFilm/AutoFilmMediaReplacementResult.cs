using System;

namespace MediaBrowser.Controller.AutoFilm;

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
