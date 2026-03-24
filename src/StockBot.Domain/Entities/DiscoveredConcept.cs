namespace StockBot.Domain.Entities;

public class DiscoveredConcept
{
    public int Id { get; set; }
    public string SourceDocumentId { get; set; } = string.Empty;
    public string? AssociatedStockCode { get; set; }
    public string Keyword { get; set; } = string.Empty; // LLM 找出的新關鍵字 (例: "玻璃基板")
    public int AppearanceCount { get; set; }
    public DateTime FirstDiscoveredAt { get; set; }
    public DateTime LastSeenAt { get; set; }

    // 審核流程：使用者透過 Telegram /approve 升級成 TrackedEntity Concept
    public bool IsApprovedAndPromoted { get; set; }
}
