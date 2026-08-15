using System;
using System.IO;
using System.Linq;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Creates the common persisted representation for OpenList sidecar subtitles.
/// </summary>
internal static class AutoFilmExternalSubtitleStream
{
    private static readonly string[] SupportedExtensions =
        [".ass", ".mks", ".sami", ".smi", ".srt", ".ssa", ".sub", ".sup", ".vtt"];

    internal static bool IsSupportedPath(string path)
    {
        return SupportedExtensions.Contains(
            Path.GetExtension(path),
            StringComparer.OrdinalIgnoreCase);
    }

    internal static MediaStream Create(
        string path,
        int index,
        string? language,
        string? title,
        bool isDefault,
        bool isForced,
        bool isHearingImpaired)
    {
        var stream = new MediaStream
        {
            Type = MediaStreamType.Subtitle,
            Index = index,
            Codec = Path.GetExtension(path).TrimStart('.').ToLowerInvariant(),
            Language = language,
            Title = title,
            IsDefault = isDefault,
            IsForced = isForced,
            IsHearingImpaired = isHearingImpaired,
            IsExternal = true,
            IsExternalUrl = true,
            SupportsExternalStream = true,
            DeliveryMethod = SubtitleDeliveryMethod.External,
            Path = path
        };
        AutoFilmSubtitleCompatibility.NormalizeExternalSup(stream);
        return stream;
    }
}
