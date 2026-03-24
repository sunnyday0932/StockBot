namespace StockBot.Infrastructure.Ai;

public interface IEmbeddingService
{
    /// <summary>
    /// 計算文本的向量 Embedding（預設 1536 維，對應 OpenAI text-embedding-3-small）。
    /// </summary>
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default);
}
