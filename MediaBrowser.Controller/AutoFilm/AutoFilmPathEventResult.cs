namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Result of applying one OpenList path event.
/// </summary>
public sealed record AutoFilmPathEventResult(
    ulong Sequence,
    string EventId,
    string Action,
    int ItemsChanged);
