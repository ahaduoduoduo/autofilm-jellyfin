using System.Security.Cryptography;
using System.Text;
using MediaBrowser.Controller.AutoFilm;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Environment-backed inbound event token validator.
/// </summary>
public sealed class AutoFilmInboundEventAuthorizer
    : IAutoFilmInboundEventAuthorizer
{
    private readonly AutoFilmOptions _options;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AutoFilmInboundEventAuthorizer"/> class.
    /// </summary>
    /// <param name="options">AutoFilm configuration.</param>
    public AutoFilmInboundEventAuthorizer(AutoFilmOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public bool IsConfigured =>
        !string.IsNullOrEmpty(_options.JellyfinInboundToken);

    /// <inheritdoc />
    public bool Validate(string token)
    {
        if (!IsConfigured)
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(
            _options.JellyfinInboundToken);
        var suppliedBytes = Encoding.UTF8.GetBytes(token);
        return expectedBytes.Length == suppliedBytes.Length
            && CryptographicOperations.FixedTimeEquals(
                expectedBytes,
                suppliedBytes);
    }
}
