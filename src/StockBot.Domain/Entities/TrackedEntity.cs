using StockBot.Domain.Enums;

namespace StockBot.Domain.Entities;

public class TrackedEntity
{
    public int Id { get; set; }
    public EntityType Type { get; set; }
    public string PrimaryName { get; set; } = string.Empty;
    public string? StockCode { get; set; } // 只有 Stock 類型才有，Concept/Person 為 null

    public List<EntityAlias> Aliases { get; set; } = [];
}
