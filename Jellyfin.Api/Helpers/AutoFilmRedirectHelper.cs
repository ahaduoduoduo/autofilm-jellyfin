using System;

namespace Jellyfin.Api.Helpers;

/// <summary>
/// Builds HTTP-safe redirect locations for AutoFilm remote media.
/// </summary>
internal static class AutoFilmRedirectHelper
{
    /// <summary>
    /// Returns an absolute URI with non-ASCII path characters percent-encoded.
    /// </summary>
    /// <param name="uri">The absolute remote URI.</param>
    /// <returns>An ASCII-safe HTTP Location header value.</returns>
    public static string GetLocation(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return uri.AbsoluteUri;
    }
}
