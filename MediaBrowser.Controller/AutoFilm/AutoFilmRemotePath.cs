using System;
using MediaBrowser.Model.Configuration;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Converts between Jellyfin remote paths and OpenList absolute paths.
/// </summary>
public static class AutoFilmRemotePath
{
    /// <summary>
    /// Prefix persisted in Jellyfin item and external subtitle paths.
    /// </summary>
    public const string SchemePrefix = "openlist://";

    /// <summary>
    /// Determines whether a path is managed by AutoFilm.
    /// </summary>
    /// <param name="path">Jellyfin path.</param>
    /// <returns><see langword="true"/> for an OpenList path.</returns>
    public static bool IsRemote(string? path)
    {
        return TryGetOpenListPath(path, out _);
    }

    /// <summary>
    /// Normalizes one library source into its persistent path representation.
    /// </summary>
    /// <param name="pathInfo">Library path information.</param>
    public static void NormalizeMediaPath(MediaPathInfo pathInfo)
    {
        ArgumentNullException.ThrowIfNull(pathInfo);

        if (pathInfo.SourceType == MediaPathSourceType.OpenList
            && !IsRemote(pathInfo.Path))
        {
            pathInfo.Path = FromOpenListPath(pathInfo.Path);
        }

        if (IsRemote(pathInfo.Path))
        {
            pathInfo.SourceType = MediaPathSourceType.OpenList;
        }
        else
        {
            pathInfo.SourceType = MediaPathSourceType.Local;
        }
    }

    /// <summary>
    /// Determines whether one path is inside a configured OpenList root.
    /// </summary>
    /// <param name="path">OpenList absolute path.</param>
    /// <param name="root">OpenList absolute root path.</param>
    /// <returns>Whether the path belongs to the root.</returns>
    public static bool IsWithinOpenListRoot(string path, string root)
    {
        if (root == "/")
        {
            return path.StartsWith('/');
        }

        return string.Equals(path, root, StringComparison.Ordinal)
            || (path.StartsWith(root, StringComparison.Ordinal)
                && path.Length > root.Length
                && path[root.Length] == '/');
    }

    /// <summary>
    /// Creates a Jellyfin remote path from an OpenList absolute path.
    /// </summary>
    /// <param name="openListPath">OpenList absolute path.</param>
    /// <returns>The path persisted by Jellyfin.</returns>
    public static string FromOpenListPath(string openListPath)
    {
        if (!TryNormalizeOpenListPath(openListPath, out var normalized))
        {
            throw new ArgumentException(
                "OpenList path must be absolute and must not contain dot segments.",
                nameof(openListPath));
        }

        return SchemePrefix + normalized;
    }

    /// <summary>
    /// Extracts an OpenList absolute path from a Jellyfin remote path.
    /// </summary>
    /// <param name="remotePath">Jellyfin remote path.</param>
    /// <param name="openListPath">Normalized OpenList path.</param>
    /// <returns>Whether conversion succeeded.</returns>
    public static bool TryGetOpenListPath(
        string? remotePath,
        out string openListPath)
    {
        openListPath = string.Empty;
        if (string.IsNullOrWhiteSpace(remotePath)
            || !remotePath.StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return TryNormalizeOpenListPath(
            remotePath[SchemePrefix.Length..],
            out openListPath);
    }

    private static bool TryNormalizeOpenListPath(
        string path,
        out string normalized)
    {
        normalized = string.Empty;
        var candidate = path.Replace('\\', '/').Trim();
        if (!candidate.StartsWith('/'))
        {
            return false;
        }

        var segments = candidate.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                return false;
            }
        }

        normalized = segments.Length == 0
            ? "/"
            : "/" + string.Join('/', segments);
        return true;
    }
}
