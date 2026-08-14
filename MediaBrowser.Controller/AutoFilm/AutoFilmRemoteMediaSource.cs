using System;
using MediaBrowser.Model.Dto;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Identifies the AutoFilm dynamic media source.
/// </summary>
public static class AutoFilmRemoteMediaSource
{
    /// <summary>
    /// Prefix used for remote media source identifiers.
    /// </summary>
    public const string MediaSourceIdPrefix = "autofilm:";

    /// <summary>
    /// Determines whether a media source is supplied by the AutoFilm OpenList integration.
    /// </summary>
    /// <param name="mediaSource">Media source.</param>
    /// <returns>Whether the media source belongs to AutoFilm OpenList.</returns>
    public static bool IsAutoFilm(MediaSourceInfo mediaSource)
    {
        return AutoFilmRemotePath.IsRemote(mediaSource.Path)
            || mediaSource.Id?.StartsWith(MediaSourceIdPrefix, StringComparison.Ordinal) == true;
    }
}
