using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Events;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Repairs incomplete OpenList media information after metadata refreshes.
/// </summary>
public sealed class AutoFilmRemoteMediaHealthMonitor : IHostedService
{
    private readonly IProviderManager _providerManager;
    private readonly IMediaStreamRepository _mediaStreamRepository;
    private readonly IAutoFilmRemoteProbeQueue _probeQueue;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AutoFilmRemoteMediaHealthMonitor" /> class.
    /// </summary>
    /// <param name="providerManager">Jellyfin metadata provider manager.</param>
    /// <param name="mediaStreamRepository">Jellyfin media stream repository.</param>
    /// <param name="probeQueue">Rate-limited OpenList media probe queue.</param>
    public AutoFilmRemoteMediaHealthMonitor(
        IProviderManager providerManager,
        IMediaStreamRepository mediaStreamRepository,
        IAutoFilmRemoteProbeQueue probeQueue)
    {
        _providerManager = providerManager;
        _mediaStreamRepository = mediaStreamRepository;
        _probeQueue = probeQueue;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _providerManager.RefreshCompleted += OnRefreshCompleted;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _providerManager.RefreshCompleted -= OnRefreshCompleted;
        return Task.CompletedTask;
    }

    internal void Inspect(BaseItem item)
    {
        if (item is not Video video
            || !AutoFilmRemotePath.IsRemote(video.Path))
        {
            return;
        }

        var mediaStreams = _mediaStreamRepository.GetMediaStreams(
            new MediaStreamQuery { ItemId = video.Id });
        if (AutoFilmRemoteProbeQueue.RequiresProbe(
                video,
                mediaStreams,
                false))
        {
            _probeQueue.Enqueue(video.Id, false);
        }
    }

    private void OnRefreshCompleted(
        object? sender,
        GenericEventArgs<BaseItem> eventArgs)
    {
        Inspect(eventArgs.Argument);
    }
}
