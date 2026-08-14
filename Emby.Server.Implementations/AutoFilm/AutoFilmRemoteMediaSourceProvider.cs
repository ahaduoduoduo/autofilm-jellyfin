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
    private readonly IAutoFilmRemoteProbeQueue _probeQueue;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AutoFilmRemoteMediaSourceProvider"/> class.
    /// </summary>
    /// <param name="mediaStreamRepository">Jellyfin media stream repository.</param>
    /// <param name="probeQueue">Rate-limited OpenList media probe queue.</param>
    public AutoFilmRemoteMediaSourceProvider(
        IMediaStreamRepository mediaStreamRepository,
        IAutoFilmRemoteProbeQueue probeQueue)
    {
        _mediaStreamRepository = mediaStreamRepository;
        _probeQueue = probeQueue;
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
        if (item is Video video
            && AutoFilmRemoteProbeQueue.RequiresProbe(
                video,
                mediaStreams,
                false))
        {
            _probeQueue.Enqueue(item.Id, false);
        }

        foreach (var stream in mediaStreams)
        {
            AutoFilmSubtitleCompatibility.NormalizeExternalSup(stream);
        }

        return Task.FromResult<IEnumerable<MediaSourceInfo>>(
        [
            new MediaSourceInfo
            {
                // Keep the dynamic source identifier equal to Jellyfin's stable item source
                // identifier. Infuse associates PlaybackInfo subtitle streams with the
                // MediaSources returned by item metadata using this value.
                Id = item.Id.ToString("N"),
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
