using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StockBot.Domain.Entities;
using StockBot.Infrastructure.Persistence;

namespace StockBot.Infrastructure.Processing;

public sealed class TopDownMatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<TopDownMatcher> logger) : ITopDownMatcher
{
    private AhoCorasickTrie?       _trie;
    private Dictionary<int, string?> _entityCodeMap = []; // entityId → StockCode

    /// <summary>
    /// 從 DB 載入所有 EntityAlias，重建 Aho-Corasick Trie。
    /// Workers 啟動時呼叫一次；白名單變更後可再次呼叫熱更新。
    /// </summary>
    public async Task RebuildAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StockBotDbContext>();

        var aliases = await db.EntityAliases
            .AsNoTracking()
            .Select(a => new { a.Keyword, a.EntityId })
            .ToListAsync(ct);

        var entityCodeMap = await db.TrackedEntities
            .AsNoTracking()
            .Select(e => new { e.Id, e.StockCode })
            .ToDictionaryAsync(e => e.Id, e => e.StockCode, ct);

        var trie = new AhoCorasickTrie();
        trie.Build(aliases.Select(a => (a.Keyword, a.EntityId)));

        // 原子替換：避免 Match 讀到半建構狀態
        _trie          = trie;
        _entityCodeMap = entityCodeMap;

        logger.LogInformation(
            "TopDownMatcher rebuilt: {Aliases} aliases, {Entities} entities.",
            aliases.Count, entityCodeMap.Count);
    }

    /// <summary>
    /// 對文件的標題 + 內文進行 Aho-Corasick 比對，回傳命中結果。
    /// 若 RebuildAsync 尚未呼叫過，拋出 InvalidOperationException。
    /// </summary>
    public AnalysisResult Match(SourceDocument document)
    {
        if (_trie is null)
            throw new InvalidOperationException(
                "TopDownMatcher is not initialized. Call RebuildAsync first.");

        // 標題通常是訊號密集處，與內文合併後一起比對
        var text = $"{document.Title} {document.Content}";

        // 聚合：entityId → mention count
        var counts = new Dictionary<int, int>();
        foreach (var (entityId, _) in _trie.Search(text))
            counts[entityId] = counts.GetValueOrDefault(entityId) + 1;

        var matches = counts
            .Select(kv => new EntityMatch
            {
                EntityId     = kv.Key,
                StockCode    = _entityCodeMap.GetValueOrDefault(kv.Key),
                MentionCount = kv.Value,
            })
            .ToList();

        return new AnalysisResult
        {
            DocumentId      = document.DocumentId,
            ProcessedAt     = DateTime.UtcNow,
            MatchedEntities = matches,
        };
    }
}
