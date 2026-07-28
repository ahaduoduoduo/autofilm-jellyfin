using System.Collections.Generic;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// A bounded migration result.
/// </summary>
public sealed record AutoFilmMigrationResult(
    bool Applied,
    int Examined,
    int ItemsMigrated,
    int SubtitlePathsMigrated,
    int SubtitleCodecsNormalized,
    int LibraryPathsMigrated,
    int Failed,
    IReadOnlyList<AutoFilmMigrationEntry> Entries,
    IReadOnlyList<AutoFilmLibraryPathMigrationEntry> LibraryPaths);
