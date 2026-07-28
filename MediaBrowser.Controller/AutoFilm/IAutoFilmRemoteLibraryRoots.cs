using System.Collections.Generic;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Reads the OpenList roots configured as Jellyfin media library sources.
/// </summary>
public interface IAutoFilmRemoteLibraryRoots
{
    /// <summary>
    /// Gets all configured OpenList absolute root paths.
    /// </summary>
    /// <returns>Normalized OpenList roots.</returns>
    IReadOnlyList<string> GetRoots();

    /// <summary>
    /// Finds the most specific configured root containing a path.
    /// </summary>
    /// <param name="openListPath">Normalized OpenList absolute path.</param>
    /// <returns>The containing root, or <see langword="null"/>.</returns>
    string? FindRoot(string openListPath);
}
