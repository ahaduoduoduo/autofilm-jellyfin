using System;
using Emby.Server.Implementations.AutoFilm;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.AutoFilm;

public sealed class AutoFilmRemoteProviderTargetResolverTests
{
    [Fact]
    public void FindOwningSeries_SeriesTarget_ReturnsTarget()
    {
        var series = new Series { Id = Guid.NewGuid() };

        var result = AutoFilmRemoteProviderTargetResolver.FindOwningSeries(
            series,
            Mock.Of<ILibraryManager>());

        Assert.Same(series, result);
    }

    [Fact]
    public void FindOwningSeries_NestedReleaseFolder_ReturnsNearestSeries()
    {
        var series = new Series { Id = Guid.NewGuid() };
        var releaseSeason = new Season
        {
            Id = Guid.NewGuid(),
            ParentId = series.Id,
            SeriesId = series.Id
        };
        var seasonDirectory = new Folder
        {
            Id = Guid.NewGuid(),
            ParentId = releaseSeason.Id
        };
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(instance => instance.GetItemById(releaseSeason.Id))
            .Returns(releaseSeason);
        libraryManager
            .Setup(instance => instance.GetItemById(series.Id))
            .Returns(series);

        var result = AutoFilmRemoteProviderTargetResolver.FindOwningSeries(
            seasonDirectory,
            libraryManager.Object);

        Assert.Same(series, result);
    }

    [Fact]
    public void Resolve_NestedTelevisionTarget_ReturnsOwningSeries()
    {
        var series = new Series { Id = Guid.NewGuid() };
        var releaseSeason = new Season
        {
            Id = Guid.NewGuid(),
            ParentId = series.Id,
            SeriesId = series.Id
        };
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(instance => instance.GetItemById(series.Id))
            .Returns(series);

        var result = AutoFilmRemoteProviderTargetResolver.Resolve(
            releaseSeason,
            new AutoFilmRemoteRefreshRequest(),
            new AutoFilmDirectorySnapshot(),
            libraryManager.Object);

        Assert.Same(series, result);
    }

    [Fact]
    public void QueueMetadataRefreshes_ProviderIdsPresent_UsesFullRefresh()
    {
        var target = new Folder { Id = Guid.NewGuid() };
        var episode = new Episode { Id = Guid.NewGuid() };
        var providerManager = new Mock<IProviderManager>();

        AutoFilmRemoteProviderTargetResolver.QueueMetadataRefreshes(
            providerManager.Object,
            target,
            new Video[] { episode },
            new AutoFilmDirectorySnapshot(),
            true);

        providerManager.Verify(instance => instance.QueueRefresh(
            target.Id,
            It.Is<MetadataRefreshOptions>(options =>
                options.MetadataRefreshMode == MetadataRefreshMode.FullRefresh
                && options.ImageRefreshMode == MetadataRefreshMode.FullRefresh
                && !options.ReplaceAllMetadata),
            RefreshPriority.High));
        providerManager.Verify(instance => instance.QueueRefresh(
            episode.Id,
            It.Is<MetadataRefreshOptions>(options =>
                options.MetadataRefreshMode == MetadataRefreshMode.FullRefresh
                && options.ImageRefreshMode == MetadataRefreshMode.FullRefresh
                && options.ReplaceAllMetadata),
            RefreshPriority.Normal));
    }

    [Fact]
    public void FindOwningSeries_ParentCycle_ReturnsNull()
    {
        var first = new Folder { Id = Guid.NewGuid() };
        var second = new Folder { Id = Guid.NewGuid() };
        first.ParentId = second.Id;
        second.ParentId = first.Id;
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(instance => instance.GetItemById(first.Id))
            .Returns(first);
        libraryManager
            .Setup(instance => instance.GetItemById(second.Id))
            .Returns(second);

        var result = AutoFilmRemoteProviderTargetResolver.FindOwningSeries(
            first,
            libraryManager.Object);

        Assert.Null(result);
    }
}
