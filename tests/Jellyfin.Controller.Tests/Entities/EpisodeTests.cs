using System;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using Xunit;

namespace Jellyfin.Controller.Tests.Entities;

public sealed class EpisodeTests
{
    [Fact]
    public void ResolveSeasonByNumber_MismatchedPhysicalWrapper_UsesLogicalSeason()
    {
        var physicalWrapper = new Season
        {
            Id = Guid.NewGuid(),
            IndexNumber = 1
        };
        var logicalSeason = new Season
        {
            Id = Guid.NewGuid(),
            IndexNumber = 2
        };
        var series = new Series
        {
            Children = new BaseItem[] { physicalWrapper, logicalSeason }
        };

        var result = Episode.ResolveSeasonByNumber(
            physicalWrapper,
            series,
            2);

        Assert.Same(logicalSeason, result);
    }

    [Fact]
    public void ResolveSeasonByNumber_NoLogicalSeason_KeepsPhysicalParent()
    {
        var physicalWrapper = new Season
        {
            Id = Guid.NewGuid(),
            IndexNumber = 1
        };
        var series = new Series
        {
            Children = new BaseItem[] { physicalWrapper }
        };

        var result = Episode.ResolveSeasonByNumber(
            physicalWrapper,
            series,
            2);

        Assert.Same(physicalWrapper, result);
    }
}
