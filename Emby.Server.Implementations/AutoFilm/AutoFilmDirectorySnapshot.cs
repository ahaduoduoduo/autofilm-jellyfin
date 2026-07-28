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
