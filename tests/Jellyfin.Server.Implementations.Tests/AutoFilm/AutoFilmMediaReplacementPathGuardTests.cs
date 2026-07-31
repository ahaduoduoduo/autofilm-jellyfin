using Emby.Server.Implementations.AutoFilm;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.AutoFilm;

public sealed class AutoFilmMediaReplacementPathGuardTests
{
    [Theory]
    [InlineData(
        "/cloud/library/A2/Example Film 1988/Example Film.mkv",
        "/cloud/library/A2/Example.Film.1988/Example.Film.mkv",
        true)]
    [InlineData(
        "/cloud/library/A2/Example Film 1988/Example Film.mkv",
        "/cloud/library/A3/Example.Film.1988/Example.Film.mkv",
        false)]
    [InlineData(
        "/cloud/library/A2/Movie 1988/Movie.mkv",
        "/cloud/library/A2/Movie.1989/Movie.mkv",
        false)]
    [InlineData(
        "/cloud/library/A2/Movie 1988/Movie.mkv",
        "/cloud/library/A2/movie.1988/movie.mkv",
        false)]
    [InlineData(
        "/cloud/library/A2/Movie-Part/Movie.mkv",
        "/cloud/library/A2/MoviePart/Movie.mkv",
        false)]
    public void AreSeparatorEquivalent_UsesOnlyNarrowSeparatorNormalization(
        string recorded,
        string actual,
        bool expected)
    {
        Assert.Equal(
            expected,
            AutoFilmMediaReplacementPathGuard.AreSeparatorEquivalent(
                recorded,
                actual));
    }

    [Theory]
    [InlineData(8_529_735_680, 8_529_735_788, true)]
    [InlineData(8_529_735_680, 8_530_784_256, true)]
    [InlineData(8_529_735_680, 8_530_784_257, false)]
    public void HasCompatibleSize_UsesOneMebibyteTolerance(
        long recorded,
        long actual,
        bool expected)
    {
        Assert.Equal(
            expected,
            AutoFilmMediaReplacementPathGuard.HasCompatibleSize(
                recorded,
                actual));
    }
}
