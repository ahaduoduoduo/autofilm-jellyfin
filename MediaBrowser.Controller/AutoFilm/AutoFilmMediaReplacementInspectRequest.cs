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
