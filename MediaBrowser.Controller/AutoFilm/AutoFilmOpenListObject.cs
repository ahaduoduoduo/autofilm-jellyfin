using System;
using System.Text.Json.Serialization;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Path-based OpenList object returned by the AutoFilm internal API.
/// </summary>
public sealed record AutoFilmOpenListObject
{
    /// <summary>
    /// Gets the full OpenList path.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the object name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the size.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; init; }

    /// <summary>
    /// Gets a value indicating whether this object is a directory.
    /// </summary>
    [JsonPropertyName("is_dir")]
    public bool IsDirectory { get; init; }

    /// <summary>
    /// Gets the remote modification time.
    /// </summary>
    [JsonPropertyName("modified")]
    public DateTime Modified { get; init; }

    /// <summary>
    /// Gets the remote creation time.
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; init; }

    /// <summary>
    /// Gets the signed OpenList download path.
    /// </summary>
    [JsonPropertyName("download_path")]
    public string? DownloadPath { get; init; }
}
