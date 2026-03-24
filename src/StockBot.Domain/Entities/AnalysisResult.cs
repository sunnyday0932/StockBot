namespace StockBot.Domain.Entities;

public class AnalysisResult
{
    public string DocumentId { get; set; } = string.Empty; // 對應 SourceDocument.DocumentId
    public DateTime ProcessedAt { get; set; }

    public List<EntityMatch> MatchedEntities { get; set; } = [];

    // LLM 計算的全文情緒分數 (-1 ~ 1)，未呼叫 LLM 時為 null
    public float? SentimentScore { get; set; }

    // 文本向量 Embedding，存入 pgvector；未計算時為 null
    public float[]? VectorEmbedding { get; set; }
}
