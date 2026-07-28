using System;
using System.IO;
using System.Threading.Tasks;
using Emby.Server.Implementations.AutoFilm;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.AutoFilm;

public sealed class AutoFilmSubtitleServiceTests
{
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
