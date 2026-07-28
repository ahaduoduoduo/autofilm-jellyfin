namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Result for one media library root path during migration.
/// </summary>
public sealed record AutoFilmLibraryPathMigrationEntry(
    string LibraryName,
    string PreviousPath,
    string MigratedPath,
    string State,
    string? Error);
