using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Supplies a direct-play-only media source for mapped AutoFilm items.
/// </summary>
public sealed class AutoFilmRemoteMediaSourceProvider : IMediaSourceProvider
{
    private readonly IMediaStreamRepository _mediaStreamRepository;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AutoFilmRemoteMediaSourceProvider"/> class.
    /// </summary>
    /// <param name="mediaStreamRepository">Jellyfin media stream repository.</param>
    public AutoFilmRemoteMediaSourceProvider(
        IMediaStreamRepository mediaStreamRepository)
    {
        _mediaStreamRepository = mediaStreamRepository;
    }

    /// <inheritdoc />
    public Task<IEnumerable<MediaSourceInfo>> GetMediaSources(
        BaseItem item,
        CancellationToken cancellationToken)
    {
        if (!AutoFilmRemotePath.IsRemote(item.Path))
        {
            return Task.FromResult<IEnumerable<MediaSourceInfo>>([]);
        }

        var mediaStreams = _mediaStreamRepository.GetMediaStreams(
            new MediaStreamQuery { ItemId = item.Id });
        foreach (var stream in mediaStreams)
        {
            AutoFilmSubtitleCompatibility.NormalizeExternalSup(stream);
        }

        return Task.FromResult<IEnumerable<MediaSourceInfo>>(
        [
            new MediaSourceInfo
            {
                Id = AutoFilmRemoteMediaSource.MediaSourceIdPrefix + item.Id.ToString("N"),
                Name = "AutoFilm OpenList",
                Path = item.Path,
                Protocol = MediaProtocol.Http,
                Type = MediaSourceType.Default,
                Container = Path.GetExtension(item.Path).TrimStart('.'),
                RunTimeTicks = item.RunTimeTicks,
                IsRemote = true,
                SupportsDirectPlay = true,
                SupportsDirectStream = false,
                SupportsTranscoding = false,
                SupportsProbing = false,
                MediaStreams = mediaStreams
            }
        ]);
    }

    /// <inheritdoc />
    public Task<ILiveStream> OpenMediaSource(
        string openToken,
        List<ILiveStream> currentLiveStreams,
        CancellationToken cancellationToken)
    {
        return Task.FromException<ILiveStream>(
            new NotSupportedException("AutoFilm media sources are direct-play only."));
    }
}
