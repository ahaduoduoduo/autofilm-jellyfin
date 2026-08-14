using System;
using System.Globalization;
using System.Threading.Tasks;
using Emby.Server.Implementations.AutoFilm;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.AutoFilm;

public sealed class AutoFilmRemoteMediaSourceProviderTests
{
    [Fact]
    public async Task GetMediaSources_RemoteItem_PreservesStableItemSourceId()
    {
        var itemId = Guid.NewGuid();
        var repository = new Mock<IMediaStreamRepository>();
        repository
            .Setup(instance => instance.GetMediaStreams(
                It.Is<MediaStreamQuery>(query => query.ItemId.Equals(itemId))))
            .Returns([]);
        var provider = new AutoFilmRemoteMediaSourceProvider(repository.Object);
        var movie = new Movie
        {
            Id = itemId,
            Path = "openlist:///115/movie/example.mkv"
        };

        var sources = await provider.GetMediaSources(
            movie,
            TestContext.Current.CancellationToken);

        var source = Assert.Single(sources);
        Assert.Equal(itemId.ToString("N", CultureInfo.InvariantCulture), source.Id);
        Assert.Equal(movie.Path, source.Path);
        Assert.True(source.SupportsDirectPlay);
        Assert.False(source.SupportsTranscoding);
    }
}
