using StockBot.Domain.Enums;

namespace StockBot.Domain.Entities;

public class SourceDocument
{
    public string DocumentId { get; set; } = string.Empty; // 唯一識別碼 (如 PTT 文章代碼)
    public SourceType SourceType { get; set; }
    public string? Author { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string? Url { get; set; }

    // PTT 專屬欄位（非 PTT 來源時為 null）
    public int? PttUpvoteCount { get; set; }
    public int? PttDownvoteCount { get; set; }
    public int? PttArrowCount { get; set; }

    // Processing 層設置：null = 尚未處理，有值 = 已由 TopDownMatcher 處理完畢
    public DateTime? ProcessedAt { get; set; }

    // TopDownMatcher 命中的 Entity 數量（0 = 無任何股票/概念提及）
    public int EntityMatchCount { get; set; }

    // BottomUpProbe 層設置：null = 尚未探針，有值 = LLM 已萃取概念
    public DateTime? ProbedAt { get; set; }
}
