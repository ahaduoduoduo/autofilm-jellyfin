using System.Collections.Generic;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Read-only discovery result.
/// </summary>
public sealed record AutoFilmMediaReplacementInspectResult(
    string RequestedPath,
    int DirectoriesRead,
    int ObjectsRead,
    IReadOnlyList<AutoFilmMediaReplacementCandidate> Candidates);
