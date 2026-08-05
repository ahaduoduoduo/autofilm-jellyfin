using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Requests a precise path-based refresh from AutoFilm Core or OpenList.
/// </summary>
public sealed record AutoFilmRemoteRefreshRequest
{
    /// <summary>
    /// Gets the OpenList absolute path or OpenList URI.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether OpenList should refresh provider data.
    /// </summary>
    [JsonPropertyName("refresh")]
    public bool Refresh { get; init; }

    /// <summary>
    /// Gets a value indicating whether a target directory is loaded recursively.
    /// </summary>
    [JsonPropertyName("recursive")]
    public bool Recursive { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether existing stream data should be reprobed.
    /// </summary>
    [JsonPropertyName("force_probe")]
    public bool ForceProbe { get; init; }

    /// <summary>
    /// Gets the scan mode: <c>new</c> adds missing items, while <c>full</c>
    /// reconciles the selected path with a fresh OpenList snapshot.
    /// </summary>
    [JsonPropertyName("scan_mode")]
    public string ScanMode { get; init; } = AutoFilmRemoteScanMode.New;

    /// <summary>
    /// Gets optional metadata provider identifiers such as Tmdb or Imdb.
    /// </summary>
    [JsonPropertyName("provider_ids")]
    public IReadOnlyDictionary<string, string>? ProviderIds { get; init; }

    /// <summary>
    /// Gets the media kind that should receive provider identifiers.
    /// </summary>
    [JsonPropertyName("provider_target")]
    public string? ProviderTarget { get; init; }
}
