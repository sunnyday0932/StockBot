namespace StockBot.Domain.Entities;

public class EntityAlias
{
    public int Id { get; set; }
    public int EntityId { get; set; }
    public string Keyword { get; set; } = string.Empty; // Aho-Corasick 實際搜尋用的關鍵字

    public TrackedEntity Entity { get; set; } = null!;
}
