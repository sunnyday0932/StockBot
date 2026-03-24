using Microsoft.Extensions.Logging;

namespace StockBot.Infrastructure.Ai;

/// <summary>
/// Embedding 服務的 Stub 實作，回傳全零向量。
/// TODO: 替換為真實實作（OpenAI text-embedding-3-small 或 Azure OpenAI）：
///   1. 安裝 Semantic Kernel 或 Azure.AI.OpenAI NuGet
///   2. 注入 API Key / Endpoint（透過 IOptions&lt;EmbeddingOptions&gt;）
///   3. 呼叫 embeddingClient.GenerateEmbeddingAsync(text)
///   4. 將 ReadOnlyMemory&lt;float&gt; 轉為 float[]
/// </summary>
internal sealed class StubEmbeddingService(ILogger<StubEmbeddingService> logger) : IEmbeddingService
{
    private const int Dimensions = 1536;

    public Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        logger.LogDebug(
            "StubEmbeddingService: returning zero vector ({Dim} dims) for text length {Len}.",
            Dimensions, text.Length);

        return Task.FromResult(new float[Dimensions]);
    }
}
