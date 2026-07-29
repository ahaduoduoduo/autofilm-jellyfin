using System;

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
