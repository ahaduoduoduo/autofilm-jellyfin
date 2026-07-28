using System.Text.Json.Serialization;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Path-only object event delivered actively by OpenList.
/// </summary>
public sealed record AutoFilmPathEvent
{
    /// <summary>
    /// Gets the stable event identifier.
    /// </summary>
    [JsonPropertyName("event_id")]
    public string EventId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the ordered delivery sequence.
    /// </summary>
    [JsonPropertyName("seq")]
    public ulong Sequence { get; init; }

    /// <summary>
    /// Gets the event type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current or removed OpenList path.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the previous OpenList path for a move.
    /// </summary>
    [JsonPropertyName("old_path")]
    public string? OldPath { get; init; }

    /// <summary>
    /// Gets a value indicating whether the object is a directory.
    /// </summary>
    [JsonPropertyName("is_dir")]
    public bool IsDirectory { get; init; }

    /// <summary>
    /// Gets the provider-neutral object version.
    /// </summary>
    [JsonPropertyName("version")]
    public long Version { get; init; }

    /// <summary>
    /// Gets the provider-neutral content tag.
    /// </summary>
    [JsonPropertyName("etag")]
    public string? ETag { get; init; }
}
