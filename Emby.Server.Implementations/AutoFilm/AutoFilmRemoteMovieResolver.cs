using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Resolves a single movie from an explicitly scanned OpenList release directory.
/// </summary>
internal static class AutoFilmRemoteMovieResolver
{
    private const int MinimumComparableNameLength = 12;

    /// <summary>
    /// Runs Jellyfin's normal resolver and applies the bounded OpenList movie
    /// directory rule only when the normal result is a folder.
    /// </summary>
    /// <param name="entry">OpenList entry.</param>
    /// <param name="parent">Persisted parent.</param>
    /// <param name="snapshot">Bounded OpenList snapshot.</param>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <returns>The resolved item.</returns>
    internal static BaseItem? ResolveEntry(
        FileSystemMetadata entry,
        Folder parent,
        AutoFilmDirectorySnapshot snapshot,
        ILibraryManager libraryManager)
    {
        var resolved = libraryManager.ResolvePath(
            entry,
            parent,
            snapshot,
            libraryManager.GetContentType(parent));
        if (resolved is not Folder releaseFolder)
        {
            return resolved;
        }

        return Resolve(
            entry,
            releaseFolder,
            parent,
            snapshot,
            libraryManager)
            ?? resolved;
    }

    /// <summary>
    /// Resolves a movie only when the containing OpenList library is a movie library
    /// and one primary video can be selected without guessing between real versions.
    /// </summary>
    /// <param name="directoryEntry">Release directory entry.</param>
    /// <param name="releaseFolder">Resolved but not necessarily persisted release folder.</param>
    /// <param name="libraryParent">Parent stored in the movie library.</param>
    /// <param name="snapshot">Bounded OpenList snapshot.</param>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <returns>The primary movie, or <see langword="null"/> when selection is ambiguous.</returns>
    internal static Movie? Resolve(
        FileSystemMetadata directoryEntry,
        Folder releaseFolder,
        Folder libraryParent,
        AutoFilmDirectorySnapshot snapshot,
        ILibraryManager libraryManager)
    {
        ArgumentNullException.ThrowIfNull(directoryEntry);
        ArgumentNullException.ThrowIfNull(releaseFolder);
        ArgumentNullException.ThrowIfNull(libraryParent);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(libraryManager);

        var collectionType = libraryManager.GetContentType(libraryParent);
        var hasMovieOptions = collectionType is null
            && libraryManager.GetLibraryOptions(libraryParent)
                .GetTypeOptions(nameof(Movie)) is not null;
        var belongsToVirtualMovieLibrary = collectionType is null
            && libraryManager.GetVirtualFolders()?.Any(folder =>
                folder.CollectionType == CollectionTypeOptions.movies
                && folder.Locations.Any(location =>
                    AutoFilmRemotePath.TryGetOpenListPath(
                        location,
                        out var rootPath)
                    && AutoFilmRemotePath.TryGetOpenListPath(
                        libraryParent.Path,
                        out var parentPath)
                    && AutoFilmRemotePath.IsWithinOpenListRoot(
                        parentPath,
                        rootPath))) == true;
        if (!directoryEntry.IsDirectory
            || (collectionType != CollectionType.movies
                && !hasMovieOptions
                && !belongsToVirtualMovieLibrary))
        {
            return null;
        }

        var candidates = snapshot.GetFileSystemEntries(directoryEntry.FullName)
            .Where(entry => !entry.IsDirectory)
            .Select(entry => new
            {
                Entry = entry,
                Item = libraryManager.ResolvePath(
                    entry,
                    releaseFolder,
                    snapshot,
                    CollectionType.movies) as Movie
            })
            .Where(candidate => candidate.Item is not null)
            .ToArray();
        var primaryEntry = SelectPrimaryEntry(
            directoryEntry.Name,
            candidates.Select(candidate => candidate.Entry).ToArray());
        if (primaryEntry is null)
        {
            return null;
        }

        var movie = candidates
            .First(candidate => string.Equals(
                candidate.Entry.FullName,
                primaryEntry.FullName,
                StringComparison.Ordinal))
            .Item!;
        movie.IsInMixedFolder = false;
        movie.Name = directoryEntry.Name;
        return movie;
    }

    /// <summary>
    /// Selects one direct child video from a release directory.
    /// </summary>
    /// <param name="directoryName">Release directory name.</param>
    /// <param name="videoEntries">Direct child entries already resolved as movies.</param>
    /// <returns>The unambiguous primary entry, or <see langword="null"/>.</returns>
    internal static FileSystemMetadata? SelectPrimaryEntry(
        string directoryName,
        IReadOnlyList<FileSystemMetadata> videoEntries)
    {
        ArgumentNullException.ThrowIfNull(directoryName);
        ArgumentNullException.ThrowIfNull(videoEntries);

        if (videoEntries.Count == 1)
        {
            return videoEntries[0];
        }

        if (videoEntries.Count == 0)
        {
            return null;
        }

        var normalizedDirectory = Normalize(directoryName);
        var matchingEntries = videoEntries
            .Where(entry =>
            {
                var normalizedFile = Normalize(
                    Path.GetFileNameWithoutExtension(entry.Name));
                return normalizedFile.Length >= MinimumComparableNameLength
                    && normalizedDirectory.Contains(
                        normalizedFile,
                        StringComparison.Ordinal);
            })
            .ToArray();
        return matchingEntries.Length == 1 ? matchingEntries[0] : null;
    }

    private static string Normalize(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                result.Append(char.ToLowerInvariant(character));
            }
        }

        return result.ToString();
    }
}
