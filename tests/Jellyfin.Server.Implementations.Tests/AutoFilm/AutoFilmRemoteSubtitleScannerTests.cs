using System;
using System.Linq;
using Emby.Naming.Common;
using Emby.Naming.ExternalFiles;
using Emby.Server.Implementations.AutoFilm;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.AutoFilm;

public sealed class AutoFilmRemoteSubtitleScannerTests
{
    private const string DirectoryPath = "/media/Our.Little.Sister.2015.mkv";
    private const string VideoName = "Our.Little.Sister.2015.mkv";
    private const string SubtitleName = "Our.Little.Sister.2015.zh.srt";

    [Fact]
    public void BuildResult_EnumeratedDirectory_AddsMatchingRemoteSubtitle()
    {
        var snapshot = CreateSnapshot(includeSubtitle: true);
        var existingVideoStream = new MediaStream
        {
            Type = MediaStreamType.Video,
            Index = 0,
            Codec = "hevc"
        };

        var result = AutoFilmRemoteSubtitleScanner.BuildResult(
            CreateVideo(),
            [existingVideoStream],
            snapshot,
            CreateParser(out var namingOptions),
            namingOptions,
            removeMissing: false);

        Assert.NotNull(result);
        Assert.True(result.Changed);
        var subtitle = Assert.Single(result.Streams.Where(
            stream => stream.Type == MediaStreamType.Subtitle));
        Assert.Equal("srt", subtitle.Codec);
        Assert.Equal("zho", subtitle.Language);
        Assert.Equal(RemotePath(SubtitleName), subtitle.Path);
        Assert.True(subtitle.IsExternal);
        Assert.True(subtitle.IsExternalUrl);
        Assert.Equal(SubtitleDeliveryMethod.External, subtitle.DeliveryMethod);
        Assert.Equal(RemotePath(SubtitleName), Assert.Single(result.SubtitleFiles));
    }

    [Fact]
    public void BuildResult_FullScan_RemovesMissingRemoteSubtitleRecord()
    {
        var snapshot = CreateSnapshot(includeSubtitle: false);
        var missingSubtitle = AutoFilmExternalSubtitleStream.Create(
            RemotePath(SubtitleName),
            1,
            "zho",
            null,
            false,
            false,
            false);

        var result = AutoFilmRemoteSubtitleScanner.BuildResult(
            CreateVideo(RemotePath(SubtitleName)),
            [new MediaStream { Type = MediaStreamType.Video, Index = 0 }, missingSubtitle],
            snapshot,
            CreateParser(out var namingOptions),
            namingOptions,
            removeMissing: true);

        Assert.NotNull(result);
        Assert.True(result.Changed);
        Assert.DoesNotContain(result.Streams, stream =>
            stream.Type == MediaStreamType.Subtitle);
        Assert.Empty(result.SubtitleFiles);
    }

    [Fact]
    public void BuildResult_AdditiveScan_PreservesMissingRemoteSubtitleRecord()
    {
        var snapshot = CreateSnapshot(includeSubtitle: false);
        var missingSubtitle = AutoFilmExternalSubtitleStream.Create(
            RemotePath(SubtitleName),
            0,
            "zho",
            null,
            false,
            false,
            false);

        var result = AutoFilmRemoteSubtitleScanner.BuildResult(
            CreateVideo(RemotePath(SubtitleName)),
            [missingSubtitle],
            snapshot,
            CreateParser(out var namingOptions),
            namingOptions,
            removeMissing: false);

        Assert.NotNull(result);
        Assert.False(result.Changed);
        Assert.Single(result.Streams);
    }

    [Fact]
    public void BuildResult_UnlistedDirectory_DoesNotChangeSubtitleRecords()
    {
        var snapshot = new AutoFilmDirectorySnapshot();

        var result = AutoFilmRemoteSubtitleScanner.BuildResult(
            CreateVideo(),
            [],
            snapshot,
            CreateParser(out var namingOptions),
            namingOptions,
            removeMissing: true);

        Assert.Null(result);
    }

    private static AutoFilmDirectorySnapshot CreateSnapshot(bool includeSubtitle)
    {
        var snapshot = new AutoFilmDirectorySnapshot();
        snapshot.MarkDirectoryEnumerated(DirectoryPath);
        snapshot.Add(new AutoFilmOpenListObject
        {
            Path = DirectoryPath + "/" + VideoName,
            Name = VideoName,
            Size = 17_000_000_000
        });
        if (includeSubtitle)
        {
            snapshot.Add(new AutoFilmOpenListObject
            {
                Path = DirectoryPath + "/" + SubtitleName,
                Name = SubtitleName,
                Size = 99_511
            });
        }

        return snapshot;
    }

    private static Video CreateVideo(params string[] subtitleFiles)
    {
        return new Video
        {
            Id = Guid.NewGuid(),
            Path = RemotePath(VideoName),
            SubtitleFiles = subtitleFiles
        };
    }

    private static ExternalPathParser CreateParser(out NamingOptions namingOptions)
    {
        namingOptions = new NamingOptions();
        var localization = new Mock<ILocalizationManager>();
        localization
            .Setup(instance => instance.FindLanguageInfo("zh"))
            .Returns(new CultureDto("zh", "Chinese", "zh", ["zho"]));
        return new ExternalPathParser(
            namingOptions,
            localization.Object,
            DlnaProfileType.Subtitle);
    }

    private static string RemotePath(string name)
    {
        return AutoFilmRemotePath.FromOpenListPath(
            DirectoryPath + "/" + name);
    }
}
