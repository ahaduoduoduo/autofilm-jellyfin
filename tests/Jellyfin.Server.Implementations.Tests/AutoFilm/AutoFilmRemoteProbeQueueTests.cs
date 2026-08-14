using System.Collections.Generic;
using Emby.Server.Implementations.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.AutoFilm;

public sealed class AutoFilmRemoteProbeQueueTests
{
    private static readonly IReadOnlyList<MediaStream> VideoStreams =
    [
        new MediaStream
        {
            Type = MediaStreamType.Video,
            IsExternal = false
        }
    ];

    [Fact]
    public void RequiresProbe_HealthyVideo_ReturnsFalse()
    {
        var video = new Video { RunTimeTicks = 1 };

        Assert.False(AutoFilmRemoteProbeQueue.RequiresProbe(video, VideoStreams, false));
    }

    [Fact]
    public void RequiresProbe_MissingRuntime_ReturnsTrue()
    {
        var video = new Video();

        Assert.True(AutoFilmRemoteProbeQueue.RequiresProbe(video, VideoStreams, false));
    }

    [Fact]
    public void RequiresProbe_MissingEmbeddedVideoStream_ReturnsTrue()
    {
        var video = new Video { RunTimeTicks = 1 };

        Assert.True(AutoFilmRemoteProbeQueue.RequiresProbe(video, [], false));
    }

    [Fact]
    public void RequiresProbe_ForcedHealthyVideo_ReturnsTrue()
    {
        var video = new Video { RunTimeTicks = 1 };

        Assert.True(AutoFilmRemoteProbeQueue.RequiresProbe(video, VideoStreams, true));
    }
}
