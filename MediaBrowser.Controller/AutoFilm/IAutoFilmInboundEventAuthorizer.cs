namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Validates the dedicated OpenList-to-Jellyfin event token.
/// </summary>
public interface IAutoFilmInboundEventAuthorizer
{
    /// <summary>
    /// Gets a value indicating whether inbound event authentication is configured.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Validates a supplied token in constant time.
    /// </summary>
    /// <param name="token">Supplied token.</param>
    /// <returns>Whether the token is valid.</returns>
    bool Validate(string token);
}
