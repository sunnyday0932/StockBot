using StockBot.Domain.Entities;
using StockBot.Domain.Enums;

namespace StockBot.Tests.Unit.Entities;

public class TrackedEntityTests
{
    [Fact]
    public void Stock_entity_should_have_stock_code()
    {
        var entity = new TrackedEntity
        {
            Type = EntityType.Stock,
            PrimaryName = "台積電",
            StockCode = "2330"
        };

        Assert.Equal(EntityType.Stock, entity.Type);
        Assert.Equal("2330", entity.StockCode);
    }

    [Fact]
    public void Concept_entity_should_have_null_stock_code()
    {
        var entity = new TrackedEntity
        {
            Type = EntityType.Concept,
            PrimaryName = "AI",
            StockCode = null
        };

        Assert.Equal(EntityType.Concept, entity.Type);
        Assert.Null(entity.StockCode);
    }

    [Fact]
    public void Entity_aliases_should_initialize_empty()
    {
        var entity = new TrackedEntity { PrimaryName = "奇鋐", Type = EntityType.Stock };

        Assert.NotNull(entity.Aliases);
        Assert.Empty(entity.Aliases);
    }

    [Fact]
    public void Entity_can_hold_multiple_aliases()
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

        Assert.Equal(3, entity.Aliases.Count);
        Assert.Contains(entity.Aliases, a => a.Keyword == "神山");
    }
}
