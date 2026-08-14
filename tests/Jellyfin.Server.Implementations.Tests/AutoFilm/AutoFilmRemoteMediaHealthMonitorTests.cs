using System;
using Emby.Server.Implementations.AutoFilm;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.AutoFilm;

public sealed class AutoFilmRemoteMediaHealthMonitorTests
{
    [Fact]
    public void Inspect_IncompleteRemoteVideo_QueuesProbe()
    {
        var itemId = Guid.NewGuid();
        var repository = new Mock<IMediaStreamRepository>();
        repository
            .Setup(instance => instance.GetMediaStreams(
                It.Is<MediaStreamQuery>(query => query.ItemId.Equals(itemId))))
            .Returns([]);
        var probeQueue = new Mock<IAutoFilmRemoteProbeQueue>();
        var monitor = new AutoFilmRemoteMediaHealthMonitor(
            Mock.Of<IProviderManager>(),
            repository.Object,
            probeQueue.Object);

        monitor.Inspect(new Movie
        {
            Id = itemId,
            Path = "openlist:///115/movie/example.mkv"
        });

        probeQueue.Verify(
            instance => instance.Enqueue(itemId, false),
            Times.Once);
    }

    [Fact]
    public void Inspect_HealthyRemoteVideo_DoesNotQueueProbe()
    {
        var itemId = Guid.NewGuid();
        var repository = new Mock<IMediaStreamRepository>();
        repository
            .Setup(instance => instance.GetMediaStreams(
                It.Is<MediaStreamQuery>(query => query.ItemId.Equals(itemId))))
            .Returns(
            [
                new MediaStream
                {
                    Type = MediaStreamType.Video,
                    IsExternal = false
                }
            ]);
        var probeQueue = new Mock<IAutoFilmRemoteProbeQueue>();
        var monitor = new AutoFilmRemoteMediaHealthMonitor(
            Mock.Of<IProviderManager>(),
            repository.Object,
            probeQueue.Object);

        monitor.Inspect(new Movie
        {
            Id = itemId,
            Path = "openlist:///115/movie/example.mkv",
            RunTimeTicks = 1
        });

        probeQueue.Verify(
            instance => instance.Enqueue(It.IsAny<Guid>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public void Inspect_LocalVideo_DoesNotReadStreamsOrQueueProbe()
    {
        var repository = new Mock<IMediaStreamRepository>();
        var probeQueue = new Mock<IAutoFilmRemoteProbeQueue>();
        var monitor = new AutoFilmRemoteMediaHealthMonitor(
            Mock.Of<IProviderManager>(),
            repository.Object,
            probeQueue.Object);

        monitor.Inspect(new Movie
        {
            Id = Guid.NewGuid(),
            Path = "/media/movie/example.mkv"
        });

        repository.Verify(
            instance => instance.GetMediaStreams(It.IsAny<MediaStreamQuery>()),
            Times.Never);
        probeQueue.Verify(
            instance => instance.Enqueue(It.IsAny<Guid>(), It.IsAny<bool>()),
            Times.Never);
    }
}
