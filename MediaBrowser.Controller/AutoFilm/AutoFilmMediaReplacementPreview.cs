using System;

namespace MediaBrowser.Controller.AutoFilm;

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
