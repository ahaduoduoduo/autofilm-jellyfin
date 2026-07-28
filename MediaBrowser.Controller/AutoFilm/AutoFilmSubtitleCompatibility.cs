using System;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Applies the external SUP representation expected by Jellyfin clients.
/// </summary>
public static class AutoFilmSubtitleCompatibility
{
    /// <summary>
    /// Determines whether the persisted codec for an external .sup stream
    /// needs migration.
    /// </summary>
    /// <param name="stream">Jellyfin media stream.</param>
    /// <returns>Whether the stored codec must change.</returns>
    public static bool RequiresExternalSupCodecNormalization(MediaStream stream)
    {
        return IsExternalSup(stream)
            && !string.Equals(
                stream.Codec,
                "sup",
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether an external .sup stream needs response normalization.
    /// </summary>
    /// <param name="stream">Jellyfin media stream.</param>
    /// <returns>Whether normalization is required.</returns>
    public static bool RequiresExternalSupNormalization(MediaStream stream)
    {
        return IsExternalSup(stream)
            && (RequiresExternalSupCodecNormalization(stream)
                || stream.IsExternalUrl != true
                || !stream.SupportsExternalStream
                || stream.DeliveryMethod != SubtitleDeliveryMethod.External);
    }

    /// <summary>
    /// Reports an external .sup file as an externally deliverable SUP stream.
    /// </summary>
    /// <param name="stream">Jellyfin media stream.</param>
    /// <returns>Whether any value changed.</returns>
    public static bool NormalizeExternalSup(MediaStream stream)
    {
        if (!IsExternalSup(stream))
        {
            return false;
        }

        var changed = RequiresExternalSupNormalization(stream);
        stream.Codec = "sup";
        stream.IsExternalUrl = true;
        stream.SupportsExternalStream = true;
        stream.DeliveryMethod = SubtitleDeliveryMethod.External;
        return changed;
    }

    private static bool IsExternalSup(MediaStream stream)
    {
        return stream.Type == MediaStreamType.Subtitle
            && stream.IsExternal
            && !string.IsNullOrWhiteSpace(stream.Path)
            && stream.Path.EndsWith(".sup", StringComparison.OrdinalIgnoreCase);
    }
}
