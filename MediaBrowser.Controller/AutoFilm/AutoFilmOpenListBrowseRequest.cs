namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Requests one OpenList directory for the Jellyfin library source picker.
/// </summary>
public sealed record AutoFilmOpenListBrowseRequest
{
    /// <summary>
    /// Gets the OpenList absolute path or persistent OpenList URI.
    /// </summary>
    public string Path { get; init; } = "/";

    /// <summary>
    /// Gets a value indicating whether OpenList should refresh provider data.
    /// </summary>
    public bool Refresh { get; init; }
}
