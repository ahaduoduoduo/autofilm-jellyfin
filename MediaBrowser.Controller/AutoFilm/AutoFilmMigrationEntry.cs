using System;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Result for one existing Jellyfin item during migration.
/// </summary>
public sealed record AutoFilmMigrationEntry(
    Guid ItemId,
    string Name,
    string PreviousPath,
    string MigratedPath,
    string State,
    int SubtitlePaths,
    int SubtitleCodecs,
    string? Error);
