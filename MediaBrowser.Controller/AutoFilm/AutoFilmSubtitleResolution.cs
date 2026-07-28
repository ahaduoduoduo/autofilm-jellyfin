using System;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Result of resolving an external subtitle.
/// </summary>
public sealed record AutoFilmSubtitleResolution(
    string Source,
    Uri? RemoteUri,
    string? LocalPath,
    bool RecordRemoved);
