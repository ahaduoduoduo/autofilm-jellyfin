using System;
using System.Linq;
using Emby.Server.Implementations.Data;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Locking;
using Jellyfin.Database.Providers.Sqlite;
using Jellyfin.Server.Implementations.Item;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using BaseItemKind = Jellyfin.Data.Enums.BaseItemKind;

namespace Jellyfin.Server.Implementations.Tests.Item;

public sealed class ItemCountServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<JellyfinDbContext> _dbOptions;
    private readonly ItemCountService _service;
    private readonly User _user;
    private readonly Guid _seasonOneId;
    private readonly Guid _seasonTwoId;

    public ItemCountServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<JellyfinDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var context = CreateDbContext())
        {
            context.Database.EnsureCreated();
        }

        var factory = new Mock<IDbContextFactory<JellyfinDbContext>>();
        factory.Setup(instance => instance.CreateDbContext()).Returns(CreateDbContext);
        var queryHelpers = new Mock<IItemQueryHelpers>();
        queryHelpers
            .Setup(instance => instance.ApplyAccessFiltering(
                It.IsAny<JellyfinDbContext>(),
                It.IsAny<IQueryable<BaseItemEntity>>(),
                It.IsAny<InternalItemsQuery>()))
            .Returns((
                JellyfinDbContext _,
                IQueryable<BaseItemEntity> query,
                InternalItemsQuery _) => query);

        var itemTypeLookup = new ItemTypeLookup();
        _service = new ItemCountService(
            factory.Object,
            itemTypeLookup,
            queryHelpers.Object);

        _user = new User("test", "auth-provider", "reset-provider")
        {
            Id = Guid.NewGuid()
        };
        _seasonOneId = Guid.NewGuid();
        _seasonTwoId = Guid.NewGuid();
        Seed(itemTypeLookup);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public void GetChildCountBatch_PhysicalWrapper_UsesLogicalSeasonIds()
    {
        var result = _service.GetChildCountBatch(
            new[] { _seasonOneId, _seasonTwoId },
            _user.Id);

        Assert.Equal(2, result[_seasonOneId]);
        Assert.Equal(2, result[_seasonTwoId]);
    }

    [Fact]
    public void GetPlayedAndTotalCountBatch_PhysicalWrapper_UsesLogicalSeasonIds()
    {
        var result = _service.GetPlayedAndTotalCountBatch(
            new[] { _seasonOneId, _seasonTwoId },
            _user);

        Assert.Equal((1, 2), result[_seasonOneId]);
        Assert.Equal((0, 2), result[_seasonTwoId]);
    }

    private void Seed(ItemTypeLookup itemTypeLookup)
    {
        var seasonType = itemTypeLookup.BaseItemKindNames[BaseItemKind.Season];
        var episodeType = itemTypeLookup.BaseItemKindNames[BaseItemKind.Episode];
        var folderType = itemTypeLookup.BaseItemKindNames[BaseItemKind.Folder];
        var seasonOne = CreateItem(_seasonOneId, seasonType, true);
        var seasonTwo = CreateItem(_seasonTwoId, seasonType, true);
        var firstPhysicalFolder = CreateItem(Guid.NewGuid(), folderType, true, _seasonOneId);
        var secondPhysicalFolder = CreateItem(Guid.NewGuid(), folderType, true, _seasonOneId);
        var episodes = new[]
        {
            CreateItem(Guid.NewGuid(), episodeType, false, firstPhysicalFolder.Id, _seasonOneId),
            CreateItem(Guid.NewGuid(), episodeType, false, firstPhysicalFolder.Id, _seasonOneId),
            CreateItem(Guid.NewGuid(), episodeType, false, secondPhysicalFolder.Id, _seasonTwoId),
            CreateItem(Guid.NewGuid(), episodeType, false, secondPhysicalFolder.Id, _seasonTwoId)
        };

        using var context = CreateDbContext();
        context.Users.Add(_user);
        context.BaseItems.AddRange(
            seasonOne,
            seasonTwo,
            firstPhysicalFolder,
            secondPhysicalFolder);
        context.BaseItems.AddRange(episodes);
        foreach (var episode in episodes)
        {
            context.AncestorIds.Add(new AncestorId
            {
                ParentItemId = _seasonOneId,
                ParentItem = seasonOne,
                ItemId = episode.Id,
                Item = episode
            });
        }

        context.UserData.Add(new UserData
        {
            ItemId = episodes[0].Id,
            Item = episodes[0],
            UserId = _user.Id,
            User = _user,
            CustomDataKey = episodes[0].Id.ToString("N"),
            Played = true
        });
        context.SaveChanges();
    }

    private static BaseItemEntity CreateItem(
        Guid id,
        string type,
        bool isFolder,
        Guid? parentId = null,
        Guid? seasonId = null)
    {
        return new BaseItemEntity
        {
            Id = id,
            Type = type,
            IsFolder = isFolder,
            IsVirtualItem = false,
            ParentId = parentId,
            SeasonId = seasonId
        };
    }

    private JellyfinDbContext CreateDbContext()
    {
        return new JellyfinDbContext(
            _dbOptions,
            NullLogger<JellyfinDbContext>.Instance,
            new SqliteDatabaseProvider(
                null!,
                NullLogger<SqliteDatabaseProvider>.Instance),
            new NoLockBehavior(NullLogger<NoLockBehavior>.Instance));
    }
}
