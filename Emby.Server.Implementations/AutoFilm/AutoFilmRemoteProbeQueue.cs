using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Runs remote ffprobe jobs one at a time with a configured minimum interval.
/// </summary>
public sealed class AutoFilmRemoteProbeQueue
    : BackgroundService, IAutoFilmRemoteProbeQueue
{
    private readonly Channel<ProbeRequest> _queue =
        Channel.CreateUnbounded<ProbeRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly ConcurrentDictionary<Guid, bool> _pending = new();
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly IMediaStreamRepository _mediaStreamRepository;
    private readonly IAutoFilmOpenListClient _openListClient;
    private readonly AutoFilmOptions _options;
    private readonly ILogger<AutoFilmRemoteProbeQueue> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoFilmRemoteProbeQueue"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="mediaEncoder">Jellyfin media encoder.</param>
    /// <param name="mediaStreamRepository">Jellyfin stream repository.</param>
    /// <param name="openListClient">OpenList path API.</param>
    /// <param name="options">AutoFilm configuration.</param>
    /// <param name="logger">Logger.</param>
    public AutoFilmRemoteProbeQueue(
        ILibraryManager libraryManager,
        IMediaEncoder mediaEncoder,
        IMediaStreamRepository mediaStreamRepository,
        IAutoFilmOpenListClient openListClient,
        AutoFilmOptions options,
        ILogger<AutoFilmRemoteProbeQueue> logger)
    {
        _libraryManager = libraryManager;
        _mediaEncoder = mediaEncoder;
        _mediaStreamRepository = mediaStreamRepository;
        _openListClient = openListClient;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Enqueue(Guid itemId, bool force)
    {
        if (_pending.AddOrUpdate(itemId, force, (_, current) => current || force)
            == force
            && _pending.Count > 0)
        {
            _queue.Writer.TryWrite(new ProbeRequest(itemId));
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var nextAllowedAt = DateTimeOffset.MinValue;
        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            if (!_pending.TryRemove(request.ItemId, out var force))
            {
                continue;
            }

            var delay = nextAllowedAt - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }

            try
            {
                await ProbeAsync(
                    request.ItemId,
                    force,
                    stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "AutoFilm remote probe failed for {ItemId}",
                    request.ItemId);
            }
            finally
            {
                nextAllowedAt = DateTimeOffset.UtcNow
                    + _options.RemoteProbeInterval;
            }
        }
    }

    private async Task ProbeAsync(
        Guid itemId,
        bool force,
        CancellationToken cancellationToken)
    {
        if (_libraryManager.GetItemById<BaseItem>(itemId) is not Video video
            || !AutoFilmRemotePath.TryGetOpenListPath(
                video.Path,
                out var openListPath))
        {
            return;
        }

        var existingStreams = _mediaStreamRepository.GetMediaStreams(
            new MediaStreamQuery { ItemId = itemId });
        if (!RequiresProbe(video, existingStreams, force))
        {
            return;
        }

        var remoteObject = await _openListClient.GetObjectAsync(
            openListPath,
            cancellationToken).ConfigureAwait(false);
        if (remoteObject is null || remoteObject.IsDirectory)
        {
            return;
        }

        var mediaInfo = await _mediaEncoder.GetMediaInfo(
            new MediaInfoRequest
            {
                ExtractChapters = false,
                MediaType = DlnaProfileType.Video,
                MediaSource = new MediaSourceInfo
                {
                    Path = _openListClient.GetInternalDownloadUri(
                        remoteObject).ToString(),
                    Protocol = MediaProtocol.Http,
                    IsRemote = true,
                    VideoType = video.VideoType
                }
            },
            cancellationToken).ConfigureAwait(false);

        var streams = mediaInfo.MediaStreams
            .Where(stream => !stream.IsExternal)
            .Concat(existingStreams.Where(stream => stream.IsExternal))
            .ToArray();
        for (var index = 0; index < streams.Length; index++)
        {
            streams[index].Index = index;
        }

        _mediaStreamRepository.SaveMediaStreams(
            itemId,
            streams,
            cancellationToken);
        video.TotalBitrate = mediaInfo.Bitrate;
        video.RunTimeTicks = mediaInfo.RunTimeTicks;
        video.Container = mediaInfo.Container;
        video.Size = remoteObject.Size;
        var videoStream = streams.FirstOrDefault(
            stream => stream.Type == MediaStreamType.Video);
        video.Width = videoStream?.Width ?? 0;
        video.Height = videoStream?.Height ?? 0;
        video.DefaultVideoStreamIndex = videoStream?.Index;
        video.HasSubtitles = streams.Any(
            stream => stream.Type == MediaStreamType.Subtitle);
        await video.UpdateToRepositoryAsync(
            ItemUpdateType.MetadataEdit,
            cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "AutoFilm remote probe saved {StreamCount} streams for {ItemId}",
            streams.Length,
            itemId);
    }

    internal static bool RequiresProbe(
        Video video,
        IReadOnlyList<MediaStream> existingStreams,
        bool force)
    {
        return force
            || video.RunTimeTicks.GetValueOrDefault() <= 0
            || !existingStreams.Any(
                stream => !stream.IsExternal
                    && stream.Type == MediaStreamType.Video);
    }

    private sealed record ProbeRequest(Guid ItemId);
}
