namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Applies an immutable preview.
/// </summary>
public sealed record AutoFilmMediaReplacementApplyRequest
{
    /// <summary>Gets the preview token.</summary>
    public string PreviewToken { get; init; } = string.Empty;
}
