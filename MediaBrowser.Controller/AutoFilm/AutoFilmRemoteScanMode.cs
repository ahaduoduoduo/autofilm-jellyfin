using System;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Defines the supported OpenList scan modes.
/// </summary>
public static class AutoFilmRemoteScanMode
{
    /// <summary>
    /// Adds missing items without removing existing database records.
    /// </summary>
    public const string New = "new";

    /// <summary>
    /// Reconciles the selected path with a fresh OpenList snapshot.
    /// </summary>
    public const string Full = "full";

    /// <summary>
    /// Normalizes and validates a requested scan mode.
    /// </summary>
    /// <param name="mode">Requested scan mode.</param>
    /// <returns>A supported scan mode.</returns>
    public static string Normalize(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)
            || string.Equals(mode, New, StringComparison.OrdinalIgnoreCase))
        {
            return New;
        }

        if (string.Equals(mode, Full, StringComparison.OrdinalIgnoreCase))
        {
            return Full;
        }

        throw new ArgumentException(
            $"Scan mode must be '{New}' or '{Full}'.",
            nameof(mode));
    }
}
