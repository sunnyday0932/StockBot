using Microsoft.EntityFrameworkCore;
using StockBot.Domain.Entities;
using StockBot.Domain.Enums;
using StockBot.Tests.Integration.Fixtures;

namespace StockBot.Tests.Integration;

[Collection("Database")]
public class TrackedEntityIntegrationTests(DatabaseFixture fixture)
{
    private readonly StockBot.Infrastructure.Persistence.StockBotDbContext _db = fixture.DbContext;

    [Fact]
    public async Task Can_save_and_retrieve_stock_entity_with_aliases()
    {
        var entity = new TrackedEntity
        {
            Type = EntityType.Stock,
            PrimaryName = "台積電",
            StockCode = "2330",
            Aliases =
            [
                new EntityAlias { Keyword = "2330" },
                new EntityAlias { Keyword = "神山" },
                new EntityAlias { Keyword = "GG" }
            ]
        };

        _db.TrackedEntities.Add(entity);
        await _db.SaveChangesAsync();

        var saved = await _db.TrackedEntities
            .Include(e => e.Aliases)
            .FirstAsync(e => e.StockCode == "2330");

        Assert.Equal("台積電", saved.PrimaryName);
        Assert.Equal(3, saved.Aliases.Count);
        Assert.Contains(saved.Aliases, a => a.Keyword == "神山");
    }

    [Fact]
    public async Task Can_save_concept_entity_without_stock_code()
    {
        var entity = new TrackedEntity
        {
            Type = EntityType.Concept,
            PrimaryName = "AI",
            StockCode = null,
            Aliases =
            [
                new EntityAlias { Keyword = "人工智慧" },
                new EntityAlias { Keyword = "AI概念" }
            ]
        };

        _db.TrackedEntities.Add(entity);
        await _db.SaveChangesAsync();

        var saved = await _db.TrackedEntities
            .Include(e => e.Aliases)
            .FirstAsync(e => e.PrimaryName == "AI");

        Assert.Equal(EntityType.Concept, saved.Type);
        Assert.Null(saved.StockCode);
        Assert.Equal(2, saved.Aliases.Count);
    }

    [Fact]
    public async Task Deleting_entity_cascades_to_aliases()
    {
        var entity = new TrackedEntity
        {
            Type = EntityType.Stock,
            PrimaryName = "測試股",
            StockCode = "9999",
            Aliases = [new EntityAlias { Keyword = "測試別名" }]
        };

        _db.TrackedEntities.Add(entity);
        await _db.SaveChangesAsync();

        _db.TrackedEntities.Remove(entity);
        await _db.SaveChangesAsync();

        var aliasCount = await _db.EntityAliases.CountAsync(a => a.EntityId == entity.Id);
        Assert.Equal(0, aliasCount);
    }
}
