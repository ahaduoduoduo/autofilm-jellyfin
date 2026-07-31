using System.Collections.Generic;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Controller.AutoFilm;

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
