using System;
using System.IO;
using MediaBrowser.Controller.AutoFilm;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Environment-backed AutoFilm configuration.
/// </summary>
public sealed class AutoFilmOptions
{
    /// <summary>
    /// Gets the OpenList base URL.
    /// </summary>
    public string OpenListUrl { get; } =
        Environment.GetEnvironmentVariable("AUTOFILM_OPENLIST_URL") ?? string.Empty;

    /// <summary>
    /// Gets the OpenList URL returned to playback clients.
    /// </summary>
    public string OpenListPublicUrl { get; } =
        Environment.GetEnvironmentVariable("AUTOFILM_OPENLIST_PUBLIC_URL")
        ?? Environment.GetEnvironmentVariable("AUTOFILM_OPENLIST_URL")
        ?? string.Empty;

    /// <summary>
    /// Gets the dedicated OpenList AutoFilm service token.
    /// </summary>
    public string OpenListToken { get; } =
        Environment.GetEnvironmentVariable("AUTOFILM_OPENLIST_TOKEN") ?? string.Empty;

    /// <summary>
    /// Gets the historical Jellyfin media path prefix.
    /// </summary>
    public string LegacyMediaPrefix { get; } =
        NormalizePrefix(
            Environment.GetEnvironmentVariable("AUTOFILM_LEGACY_MEDIA_PREFIX")
            ?? "/movie/drimnt");

    /// <summary>
    /// Gets the OpenList media path prefix.
    /// </summary>
    public string OpenListMediaPrefix { get; } =
        NormalizePrefix(
            Environment.GetEnvironmentVariable("AUTOFILM_OPENLIST_MEDIA_PREFIX")
            ?? "/");

    /// <summary>
    /// Gets the read-only legacy subtitle root mounted in the test container.
    /// </summary>
    public string LegacySubtitleRoot { get; } =
        Environment.GetEnvironmentVariable("AUTOFILM_LEGACY_SUBTITLE_ROOT")
        ?? "/legacy-subtitles";

    /// <summary>
    /// Gets the token accepted from OpenList event delivery.
    /// </summary>
    public string JellyfinInboundToken { get; } =
        Environment.GetEnvironmentVariable("AUTOFILM_JELLYFIN_INBOUND_TOKEN")
        ?? string.Empty;

    /// <summary>
    /// Gets the maximum number of remote directories read by one refresh.
    /// </summary>
    public int RemoteRefreshMaxDirectories { get; } = ParseBoundedInteger(
        Environment.GetEnvironmentVariable("AUTOFILM_REMOTE_REFRESH_MAX_DIRECTORIES"),
        64,
        1,
        512);

    /// <summary>
    /// Gets the maximum number of remote objects read by one refresh.
    /// </summary>
    public int RemoteRefreshMaxObjects { get; } = ParseBoundedInteger(
        Environment.GetEnvironmentVariable("AUTOFILM_REMOTE_REFRESH_MAX_OBJECTS"),
        5000,
        1,
        50000);

    /// <summary>
    /// Gets the minimum interval between remote ffprobe operations.
    /// </summary>
    public TimeSpan RemoteProbeInterval { get; } = TimeSpan.FromSeconds(
        ParseBoundedInteger(
            Environment.GetEnvironmentVariable("AUTOFILM_REMOTE_PROBE_INTERVAL_SECONDS"),
            30,
            5,
            3600));

    /// <summary>
    /// Gets a value indicating whether OpenList access is configured.
    /// </summary>
    public bool IsOpenListConfigured =>
        Uri.TryCreate(OpenListUrl, UriKind.Absolute, out _)
        && Uri.TryCreate(OpenListPublicUrl, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(OpenListToken);

    /// <summary>
    /// Maps an existing Jellyfin path to OpenList.
    /// </summary>
    /// <param name="legacyPath">Existing Jellyfin path.</param>
    /// <returns>The OpenList path, or <see langword="null"/>.</returns>
    public string? MapLegacyToRemote(string? legacyPath)
    {
        if (string.IsNullOrWhiteSpace(legacyPath))
        {
            return null;
        }

        var normalized = legacyPath.Replace('\\', '/');
        if (!HasPathPrefix(normalized, LegacyMediaPrefix))
        {
            return null;
        }

        var relative = normalized[LegacyMediaPrefix.Length..].TrimStart('/');
        return CombineRemotePath(OpenListMediaPrefix, relative);
    }

    /// <summary>
    /// Maps an existing Jellyfin path to the persistent OpenList URI.
    /// </summary>
    /// <param name="legacyPath">Existing Jellyfin path.</param>
    /// <returns>The remote URI, or <see langword="null"/>.</returns>
    public string? MapLegacyToRemoteUri(string? legacyPath)
    {
        var openListPath = MapLegacyToRemote(legacyPath);
        return openListPath is null
            ? null
            : AutoFilmRemotePath.FromOpenListPath(openListPath);
    }

    /// <summary>
    /// Maps a persistent OpenList URI to the read-only legacy mount.
    /// </summary>
    /// <param name="remoteUri">Jellyfin remote path.</param>
    /// <returns>The mounted fallback path, or <see langword="null"/>.</returns>
    public string? MapRemoteToLocal(string? remoteUri)
    {
        if (!AutoFilmRemotePath.TryGetOpenListPath(remoteUri, out var openListPath)
            || !HasPathPrefix(openListPath, OpenListMediaPrefix))
        {
            return null;
        }

        var relative = openListPath[OpenListMediaPrefix.Length..].TrimStart('/');
        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var result = LegacySubtitleRoot;
        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                return null;
            }

            result = Path.Combine(result, segment);
        }

        return result;
    }

    private static string NormalizePrefix(string value)
    {
        var normalized = value.Replace('\\', '/').Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized.Length > 1
            ? normalized.TrimEnd('/')
            : normalized;
    }

    private static string CombineRemotePath(string prefix, string relative)
    {
        if (string.IsNullOrEmpty(relative))
        {
            return prefix;
        }

        return prefix == "/"
            ? "/" + relative
            : prefix + "/" + relative;
    }

    private static bool HasPathPrefix(string path, string prefix)
    {
        if (prefix == "/")
        {
            return path.StartsWith('/');
        }

        return string.Equals(path, prefix, StringComparison.Ordinal)
            || (path.StartsWith(prefix, StringComparison.Ordinal)
                && path.Length > prefix.Length
                && path[prefix.Length] == '/');
    }

    private static int ParseBoundedInteger(
        string? value,
        int defaultValue,
        int minimum,
        int maximum)
    {
        return int.TryParse(value, out var parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : defaultValue;
    }
}
