using System;
using Emby.Server.Implementations.AutoFilm;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.IO;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.AutoFilm;

public sealed class AutoFilmRemoteMovieResolverTests
{
    [Fact]
    public void SelectPrimaryEntry_OneDirectVideo_SelectsIt()
    {
        var movie = Entry("Movie.2008.2160p.mkv");

        var result = AutoFilmRemoteMovieResolver.SelectPrimaryEntry(
            "Movie.2008.2160p",
            [movie]);

        Assert.Same(movie, result);
    }

    [Fact]
    public void SelectPrimaryEntry_ReleaseAndAdvertisementVideos_SelectsNameMatch()
    {
        var movie = Entry("Stalker.1979.CC.1080p.BluRay.Remux.AVC.FLAC-QuickIO.mkv");
        var advertisementMkv = Entry("更多无水印高清电影请访问.BBQDDQ.MKV");
        var advertisementMp4 = Entry("更多无水印蓝光原盘请访问.BBQDDQ.MP4");

        var result = AutoFilmRemoteMovieResolver.SelectPrimaryEntry(
            "【高清影视之家发布】潜行者.Stalker.1979.CC.1080p.BluRay.Remux.AVC.FLAC-QuickIO",
            [advertisementMkv, movie, advertisementMp4]);

        Assert.Same(movie, result);
    }

    [Fact]
    public void SelectPrimaryEntry_MultipleUnmatchedVideos_DoesNotGuessBySize()
    {
        var first = Entry("First.Cut.2160p.mkv", 80_000_000_000);
        var second = Entry("Second.Cut.1080p.mkv", 8_000_000_000);

        var result = AutoFilmRemoteMovieResolver.SelectPrimaryEntry(
            "Movie.Collection",
            [first, second]);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_TelevisionLibrary_DoesNotOverrideJellyfinResolver()
    {
        var directory = DirectoryEntry("/media/Movie.Release");
        var parent = new Folder { Path = "openlist:///media" };
        var releaseFolder = new Folder { Path = directory.FullName };
        var library = new Mock<ILibraryManager>();
        library
            .Setup(instance => instance.GetContentType(parent))
            .Returns(CollectionType.tvshows);

        var result = AutoFilmRemoteMovieResolver.Resolve(
            directory,
            releaseFolder,
            parent,
            new AutoFilmDirectorySnapshot(),
            library.Object);

        Assert.Null(result);
        library.Verify(
            instance => instance.ResolvePath(
                It.IsAny<FileSystemMetadata>(),
                It.IsAny<Folder>(),
                It.IsAny<IDirectoryService>(),
                It.IsAny<CollectionType?>()),
            Times.Never);
    }

    [Fact]
    public void Resolve_MovieWithSampleDirectory_ReturnsDirectMovie()
    {
        const string directoryPath = "/media/Synecdoche.New.York.2008.2160p";
        const string moviePath = directoryPath + "/Synecdoche.New.York.2008.2160p.mkv";
        var snapshot = new AutoFilmDirectorySnapshot();
        snapshot.Add(new AutoFilmOpenListObject
        {
            Path = directoryPath,
            Name = "Synecdoche.New.York.2008.2160p",
            IsDirectory = true
        });
        snapshot.Add(new AutoFilmOpenListObject
        {
            Path = directoryPath + "/Sample",
            Name = "Sample",
            IsDirectory = true
        });
        snapshot.Add(new AutoFilmOpenListObject
        {
            Path = moviePath,
            Name = "Synecdoche.New.York.2008.2160p.mkv",
            Size = 27_000_000_000
        });
        var directory = snapshot.GetFileSystemEntry(
            AutoFilmRemotePath.FromOpenListPath(directoryPath))!;
        var parent = new Folder { Path = "openlist:///media" };
        var releaseFolder = new Folder { Path = directory.FullName };
        var library = new Mock<ILibraryManager>();
        library
            .Setup(instance => instance.GetContentType(parent))
            .Returns((CollectionType?)null);
        library
            .Setup(instance => instance.GetLibraryOptions(parent))
            .Returns(new LibraryOptions
            {
                TypeOptions = [new TypeOptions { Type = nameof(Movie) }]
            });
        library
            .Setup(instance => instance.ResolvePath(
                It.Is<FileSystemMetadata>(entry => entry.Name.EndsWith(".mkv", StringComparison.Ordinal)),
                releaseFolder,
                snapshot,
                CollectionType.movies))
            .Returns(new Movie
            {
                Path = AutoFilmRemotePath.FromOpenListPath(moviePath),
                Name = "Synecdoche.New.York.2008.2160p.mkv",
                IsInMixedFolder = true
            });

        var result = AutoFilmRemoteMovieResolver.Resolve(
            directory,
            releaseFolder,
            parent,
            snapshot,
            library.Object);

        Assert.NotNull(result);
        Assert.Equal(AutoFilmRemotePath.FromOpenListPath(moviePath), result.Path);
        Assert.Equal(directory.Name, result.Name);
        Assert.False(result.IsInMixedFolder);
    }

    private static FileSystemMetadata Entry(string name, long length = 0)
    {
        return new FileSystemMetadata
        {
            Name = name,
            FullName = "/media/" + name,
            Length = length
        };
    }

    private static FileSystemMetadata DirectoryEntry(string path)
    {
        return new FileSystemMetadata
        {
            Name = System.IO.Path.GetFileName(path),
            FullName = path,
            IsDirectory = true
        };
    }
}
