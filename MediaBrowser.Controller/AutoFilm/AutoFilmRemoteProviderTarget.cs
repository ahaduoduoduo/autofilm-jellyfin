using System;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Defines the logical media target supplied by a trusted remote importer.
/// </summary>
public static class AutoFilmRemoteProviderTarget
{
    /// <summary>
    /// A movie metadata target.
    /// </summary>
    public const string Movie = "movie";

    /// <summary>
    /// A television series metadata target.
    /// </summary>
    public const string Series = "series";

    /// <summary>
    /// Normalizes an optional target value.
    /// </summary>
    /// <param name="value">Request value.</param>
    /// <returns>The normalized target, or <see langword="null"/>.</returns>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (string.Equals(value, Movie, StringComparison.OrdinalIgnoreCase))
        {
            return Movie;
        }

        if (string.Equals(value, Series, StringComparison.OrdinalIgnoreCase))
        {
            return Series;
        }

        throw new ArgumentException(
            $"Unsupported provider target '{value}'. Expected '{Movie}' or '{Series}'.",
            nameof(value));
    }
}
