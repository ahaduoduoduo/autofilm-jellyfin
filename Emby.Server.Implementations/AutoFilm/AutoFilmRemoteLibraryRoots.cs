using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Library;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Reads remote roots from Jellyfin's normal virtual-folder configuration.
/// </summary>
public sealed class AutoFilmRemoteLibraryRoots : IAutoFilmRemoteLibraryRoots
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AutoFilmRemoteLibraryRoots"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    public AutoFilmRemoteLibraryRoots(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetRoots()
    {
        return _libraryManager.GetVirtualFolders()
            .SelectMany(folder => folder.Locations)
            .Select(path => AutoFilmRemotePath.TryGetOpenListPath(
                    path,
                    out var openListPath)
                ? openListPath
                : null)
            .Where(path => path is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(path => path.Length)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public string? FindRoot(string openListPath)
    {
        return GetRoots().FirstOrDefault(
            root => AutoFilmRemotePath.IsWithinOpenListRoot(
                openListPath,
                root));
    }
}
