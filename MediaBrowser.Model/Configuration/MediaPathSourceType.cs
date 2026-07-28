namespace MediaBrowser.Model.Configuration;

/// <summary>
/// Identifies how Jellyfin obtains objects below a media library path.
/// </summary>
public enum MediaPathSourceType
{
    /// <summary>
    /// A path exposed by the host operating system.
    /// </summary>
    Local = 0,

    /// <summary>
    /// A path exposed by OpenList through the AutoFilm API.
    /// </summary>
    OpenList = 1
}
