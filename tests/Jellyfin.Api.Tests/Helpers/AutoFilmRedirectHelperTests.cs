using System;
using Jellyfin.Api.Helpers;
using Xunit;

namespace Jellyfin.Api.Tests.Helpers;

public static class AutoFilmRedirectHelperTests
{
    [Fact]
    public static void GetLocation_UnicodePath_ReturnsEscapedAsciiUri()
    {
        var uri = new Uri(
            "https://openlist.example:5001/d/115/movie/"
            + "%E5%A4%B4%E5%8F%B7%E7%8E%A9%E5%AE%B6/video.mkv?sign=abc");

        var location = AutoFilmRedirectHelper.GetLocation(uri);

        Assert.Contains(
            "%E5%A4%B4%E5%8F%B7%E7%8E%A9%E5%AE%B6",
            location,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("头号玩家", location, StringComparison.Ordinal);
        Assert.All(location, character => Assert.InRange((int)character, 0, 127));
    }
}
