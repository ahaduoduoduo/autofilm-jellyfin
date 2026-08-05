using System;
using MediaBrowser.Controller.AutoFilm;
using Xunit;

namespace Jellyfin.Controller.Tests.AutoFilm;

public class AutoFilmRemoteScanModeTests
{
    [Theory]
    [InlineData(null, AutoFilmRemoteScanMode.New)]
    [InlineData("", AutoFilmRemoteScanMode.New)]
    [InlineData("NEW", AutoFilmRemoteScanMode.New)]
    [InlineData("full", AutoFilmRemoteScanMode.Full)]
    public void Normalize_ValidValue_ReturnsCanonicalMode(
        string? value,
        string expected)
    {
        Assert.Equal(expected, AutoFilmRemoteScanMode.Normalize(value));
    }

    [Fact]
    public void Normalize_UnknownValue_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => AutoFilmRemoteScanMode.Normalize("replace"));
    }
}
