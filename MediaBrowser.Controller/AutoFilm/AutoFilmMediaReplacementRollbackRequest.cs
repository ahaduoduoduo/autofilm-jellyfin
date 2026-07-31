namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Restores one successful replacement.
/// </summary>
public sealed record AutoFilmMediaReplacementRollbackRequest
{
    /// <summary>Gets the rollback token returned by Apply.</summary>
    public string RollbackToken { get; init; } = string.Empty;
}
