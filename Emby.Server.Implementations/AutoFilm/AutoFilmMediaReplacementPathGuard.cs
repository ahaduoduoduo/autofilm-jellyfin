using System;
using System.Text;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Validates the narrowly scoped legacy path correction accepted by media
/// replacement previews.
/// </summary>
internal static class AutoFilmMediaReplacementPathGuard
{
    private const long MaximumSizeDifference = 1024 * 1024;

    public static bool AreSeparatorEquivalent(string recordedPath, string actualPath)
    {
        return string.Equals(
            ComparablePath(recordedPath),
            ComparablePath(actualPath),
            StringComparison.Ordinal);
    }

    public static bool HasCompatibleSize(long? recordedSize, long actualSize)
    {
        return !recordedSize.HasValue
            || recordedSize.Value <= 0
            || Math.Abs((decimal)recordedSize.Value - actualSize) <= MaximumSizeDifference;
    }

    private static string ComparablePath(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC);
        var result = new StringBuilder(normalized.Length);
        var previousWasSeparator = false;
        foreach (var character in normalized)
        {
            if (character is '.' or '_' or '-' || char.IsWhiteSpace(character))
            {
                if (!previousWasSeparator)
                {
                    result.Append(' ');
                    previousWasSeparator = true;
                }

                continue;
            }

            result.Append(character);
            previousWasSeparator = false;
        }

        return result.ToString();
    }
}
