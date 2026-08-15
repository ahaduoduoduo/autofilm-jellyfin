using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Read-only directory data used by Jellyfin resolvers for one remote refresh.
/// </summary>
internal sealed class AutoFilmDirectorySnapshot : IDirectoryService
{
    private readonly Dictionary<string, FileSystemMetadata> _entries =
        new(StringComparer.Ordinal);

    private readonly HashSet<string> _enumeratedDirectories =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Adds or replaces one OpenList object.
    /// </summary>
    /// <param name="obj">Path-based OpenList object.</param>
    public void Add(AutoFilmOpenListObject obj)
    {
        var remotePath = AutoFilmRemotePath.FromOpenListPath(obj.Path);
        _entries[remotePath] = new FileSystemMetadata
        {
            Exists = true,
            FullName = remotePath,
            Name = obj.Name,
            Extension = obj.IsDirectory ? string.Empty : Path.GetExtension(obj.Name),
            Length = obj.Size,
            IsDirectory = obj.IsDirectory,
            CreationTimeUtc = NormalizeDate(obj.Created),
            LastWriteTimeUtc = NormalizeDate(obj.Modified)
        };
    }

    /// <summary>
    /// Records that OpenList returned a complete listing for one directory.
    /// </summary>
    /// <param name="openListPath">Normalized OpenList absolute path.</param>
    public void MarkDirectoryEnumerated(string openListPath)
    {
        _enumeratedDirectories.Add(
            AutoFilmRemotePath.FromOpenListPath(openListPath));
    }

    /// <summary>
    /// Gets whether one directory was explicitly listed for this snapshot.
    /// </summary>
    /// <param name="remotePath">OpenList URI.</param>
    /// <returns>Whether the directory listing completed successfully.</returns>
    public bool WasDirectoryEnumerated(string remotePath)
    {
        return _enumeratedDirectories.Contains(remotePath);
    }

    /// <inheritdoc />
    public FileSystemMetadata[] GetFileSystemEntries(string path)
    {
        return _entries.Values
            .Where(entry => string.Equals(
                GetParent(entry.FullName),
                path,
                StringComparison.Ordinal))
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public List<FileSystemMetadata> GetDirectories(string path)
    {
        return GetFileSystemEntries(path)
            .Where(entry => entry.IsDirectory)
            .ToList();
    }

    /// <inheritdoc />
    public List<FileSystemMetadata> GetFiles(string path)
    {
        return GetFileSystemEntries(path)
            .Where(entry => !entry.IsDirectory)
            .ToList();
    }

    /// <inheritdoc />
    public FileSystemMetadata? GetFile(string path)
    {
        return GetFileSystemEntry(path) is { IsDirectory: false } entry
            ? entry
            : null;
    }

    /// <inheritdoc />
    public FileSystemMetadata? GetDirectory(string path)
    {
        return GetFileSystemEntry(path) is { IsDirectory: true } entry
            ? entry
            : null;
    }

    /// <inheritdoc />
    public FileSystemMetadata? GetFileSystemEntry(string path)
    {
        return _entries.GetValueOrDefault(path);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetFilePaths(string path)
    {
        return GetFiles(path).Select(entry => entry.FullName).ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetFilePaths(string path, bool clearCache)
    {
        return GetFilePaths(path);
    }

    /// <summary>
    /// Gets every loaded entry at or below a remote path.
    /// </summary>
    /// <param name="remotePath">Normalized OpenList URI.</param>
    /// <param name="includeRoot">Whether to include the root entry.</param>
    /// <returns>The bounded snapshot entries.</returns>
    public IReadOnlyList<FileSystemMetadata> GetEntriesWithin(
        string remotePath,
        bool includeRoot = false)
    {
        var prefix = remotePath.EndsWith('/')
            ? remotePath
            : remotePath + "/";
        return _entries.Values
            .Where(entry => (includeRoot
                    && string.Equals(
                        entry.FullName,
                        remotePath,
                        StringComparison.Ordinal))
                || entry.FullName.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(entry => entry.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public bool IsAccessible(string path)
    {
        return _entries.ContainsKey(path);
    }

    private static string GetParent(string remotePath)
    {
        if (!AutoFilmRemotePath.TryGetOpenListPath(
                remotePath,
                out var openListPath)
            || openListPath == "/")
        {
            return string.Empty;
        }

        var separator = openListPath.LastIndexOf('/');
        var parent = separator <= 0
            ? "/"
            : openListPath[..separator];
        return AutoFilmRemotePath.FromOpenListPath(parent);
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return value == default
            ? DateTime.UtcNow
            : value.ToUniversalTime();
    }
}
