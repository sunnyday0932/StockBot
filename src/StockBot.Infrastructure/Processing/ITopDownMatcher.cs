using StockBot.Domain.Entities;

namespace StockBot.Infrastructure.Processing;

public interface ITopDownMatcher
{
    /// <summary>從 DB 重新載入 EntityAlias 白名單並重建 Aho-Corasick Trie。</summary>
    Task RebuildAsync(CancellationToken ct = default);

    /// <summary>對單篇文件進行多關鍵字比對，回傳 AnalysisResult。</summary>
    AnalysisResult Match(SourceDocument document);
}
