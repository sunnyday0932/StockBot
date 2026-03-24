namespace StockBot.Domain.Entities;

public class EntityMatch
{
    public int EntityId { get; set; }           // 對應 TrackedEntity.Id
    public string? StockCode { get; set; }      // 快取，避免多次 JOIN
    public string? MatchedConcept { get; set; } // 同時命中的概念標籤
    public int MentionCount { get; set; }       // 在這篇文章中出現幾次
}
