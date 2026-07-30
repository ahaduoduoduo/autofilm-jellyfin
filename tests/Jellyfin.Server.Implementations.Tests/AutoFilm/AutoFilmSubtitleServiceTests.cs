using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.AutoFilm;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.AutoFilm;

public sealed class AutoFilmSubtitleServiceTests
{
    [Fact]
    public async Task UploadAsync_RemoteNameExists_UsesJellyfinNumberedName()
    {
        var itemId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        var movie = new Movie
        {
            Id = itemId,
            Name = "Movie",
            Path = "openlist:///media/Movie.mkv"
        };
        var library = new Mock<ILibraryManager>();
        library
            .Setup(instance => instance.GetItemById<BaseItem>(itemId))
            .Returns(movie);
        var repository = new Mock<IMediaStreamRepository>();
        repository
            .Setup(instance => instance.GetMediaStreams(
                It.Is<MediaStreamQuery>(query => query.ItemId.Equals(itemId))))
            .Returns([]);
        string? uploadedPath = null;
        var openListClient = new Mock<IAutoFilmOpenListClient>();
        openListClient
            .Setup(instance => instance.GetObjectAsync(
                It.IsAny<string>(),
                cancellationToken))
            .ReturnsAsync((string path, CancellationToken _) =>
                path == "/media/Movie.zh.ass" || path == uploadedPath
                    ? new AutoFilmOpenListObject
                    {
                        Path = path,
                        Name = Path.GetFileName(path),
                        DownloadPath = "/d/" + Path.GetFileName(path)
                    }
                    : null);
        openListClient
            .Setup(instance => instance.UploadContentAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<long?>(),
                cancellationToken))
            .Callback((string path, Stream _, long? _, CancellationToken _) =>
                uploadedPath = path)
            .Returns(Task.CompletedTask);
        openListClient
            .Setup(instance => instance.GetDownloadUri(
                It.IsAny<AutoFilmOpenListObject>()))
            .Returns(new Uri("https://openlist.example/d/subtitle"));
        using var service = new AutoFilmSubtitleService(
            openListClient.Object,
            library.Object,
            repository.Object,
            NullLogger<AutoFilmSubtitleService>.Instance);

        await using var content = new MemoryStream("subtitle"u8.ToArray());
        var result = await service.UploadAsync(
            itemId,
            "ass",
            "zh",
            false,
            false,
            content,
            content.Length,
            cancellationToken);

        Assert.NotNull(result);
        Assert.Equal("/media/Movie.zh.0.ass", uploadedPath);
        repository.Verify(
            instance => instance.SaveMediaStreams(
                itemId,
                It.Is<IReadOnlyList<MediaStream>>(streams =>
                    streams.Count == 1
                    && streams[0].Path == "openlist:///media/Movie.zh.0.ass"),
                cancellationToken),
            Times.Once);
    }

    [Theory]
    [InlineData("sup")]
    [InlineData("pgssub")]
    [InlineData(".SUP")]
    public async Task ResolveAsync_LocalExternalSup_ReturnsOriginalFile(
        string requestedFormat)
    {
        var itemId = Guid.NewGuid();
        var subtitlePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.sup");
        var cancellationToken = TestContext.Current.CancellationToken;
        await File.WriteAllBytesAsync(
            subtitlePath,
            [0x50, 0x47],
            cancellationToken);

        try
        {
            var repository = new Mock<IMediaStreamRepository>();
            repository
                .Setup(instance => instance.GetMediaStreams(
                    It.Is<MediaStreamQuery>(
                        query => query.ItemId.Equals(itemId))))
                .Returns(
                [
                    new MediaStream
                    {
                        Type = MediaStreamType.Subtitle,
                        Index = 4,
                        Codec = "sup",
                        IsExternal = true,
                        Path = subtitlePath
                    }
                ]);
            var openListClient = new Mock<IAutoFilmOpenListClient>();
            using var service = new AutoFilmSubtitleService(
                openListClient.Object,
                Mock.Of<ILibraryManager>(),
                repository.Object,
                NullLogger<AutoFilmSubtitleService>.Instance);

            var result = await service.ResolveAsync(
                itemId,
                4,
                requestedFormat,
                cancellationToken);

            Assert.NotNull(result);
            Assert.Equal("local", result.Source);
            Assert.Equal(subtitlePath, result.LocalPath);
            Assert.Null(result.RemoteUri);
            Assert.False(result.RecordRemoved);
            openListClient.VerifyNoOtherCalls();
        }
        finally
        {
            File.Delete(subtitlePath);
        }
    }

    [Fact]
    public async Task ResolveAsync_LocalExternalSupRequestedAsText_IsNotManaged()
    {
        var itemId = Guid.NewGuid();
        var subtitlePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.sup");
        var cancellationToken = TestContext.Current.CancellationToken;
        await File.WriteAllBytesAsync(
            subtitlePath,
            [0x50, 0x47],
            cancellationToken);

        try
        {
            var repository = new Mock<IMediaStreamRepository>();
            repository
                .Setup(instance => instance.GetMediaStreams(
                    It.Is<MediaStreamQuery>(
                        query => query.ItemId.Equals(itemId))))
                .Returns(
                [
                    new MediaStream
                    {
                        Type = MediaStreamType.Subtitle,
                        Index = 4,
                        Codec = "sup",
                        IsExternal = true,
                        Path = subtitlePath
                    }
                ]);
            using var service = new AutoFilmSubtitleService(
                Mock.Of<IAutoFilmOpenListClient>(),
                Mock.Of<ILibraryManager>(),
                repository.Object,
                NullLogger<AutoFilmSubtitleService>.Instance);

            var result = await service.ResolveAsync(
                itemId,
                4,
                "srt",
                cancellationToken);

            Assert.Null(result);
        }
        finally
        {
            File.Delete(subtitlePath);
        }
    }
}
