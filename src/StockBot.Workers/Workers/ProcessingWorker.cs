using Microsoft.EntityFrameworkCore;
using Pgvector;
using StockBot.Domain.Entities;
using StockBot.Infrastructure.Ai;
using StockBot.Infrastructure.InfluxDb;
using StockBot.Infrastructure.Persistence;
using StockBot.Infrastructure.Processing;

namespace StockBot.Workers.Workers;

/// <summary>
/// 定時掃描未處理的 SourceDocument（ProcessedAt IS NULL），執行：
///   1. Aho-Corasick TopDownMatcher → MentionCount 寫入 InfluxDB stock_mentions
///   2. IEmbeddingService → VectorEmbedding 寫入 PostgreSQL DocumentEmbedding
/// </summary>
public sealed class ProcessingWorker(
    ITopDownMatcher matcher,
    IInfluxDbWriter influxWriter,
    IEmbeddingService embeddingService,
    IServiceScopeFactory scopeFactory,
    ILogger<ProcessingWorker> logger) : BackgroundService
{
    private const int BatchSize           = 50;
    private const int PollIntervalSeconds = 30;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ProcessingWorker: building Aho-Corasick trie from whitelist...");
        await matcher.RebuildAsync(stoppingToken);
        logger.LogInformation("ProcessingWorker: trie ready, starting processing loop.");

        await ProcessBatchAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(PollIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ProcessBatchAsync(stoppingToken);
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StockBotDbContext>();

            var docs = await db.SourceDocuments
                .Where(d => d.ProcessedAt == null)
                .OrderBy(d => d.PublishedAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (docs.Count == 0)
            {
                logger.LogDebug("ProcessingWorker: no unprocessed documents.");
                return;
            }

            logger.LogInformation("ProcessingWorker: processing {Count} documents.", docs.Count);

            var hits       = new List<(SourceDocument Doc, AnalysisResult Result)>();
            var embeddings = new List<DocumentEmbedding>();
            var now        = DateTime.UtcNow;

            foreach (var doc in docs)
            {
                if (ct.IsCancellationRequested) break;

                // Step 1: TopDownMatcher
                var result = matcher.Match(doc);
                doc.ProcessedAt      = now;
                doc.EntityMatchCount = result.MatchedEntities.Count;

                if (result.MatchedEntities.Count > 0)
                {
                    hits.Add((doc, result));
                    logger.LogInformation(
                        "Processing [{DocId}] → {Count} entities matched.",
                        doc.DocumentId, result.MatchedEntities.Count);
                }

                // Step 2: Embedding（命中文章才計算，節省 API 費用）
                if (result.MatchedEntities.Count > 0)
                {
                    var text   = $"{doc.Title} {doc.Content}";
                    var vector = await embeddingService.GetEmbeddingAsync(text, ct);

                    embeddings.Add(new DocumentEmbedding
                    {
                        DocumentId     = doc.DocumentId,
                        Embedding      = new Vector(vector),
                        SentimentScore = null, // 由 BottomUpProbe（LLM）填入
                        ProcessedAt    = now,
                    });
                }
            }

            // 寫 InfluxDB mentions
            if (hits.Count > 0)
                await influxWriter.WriteMentionsAsync(hits, ct);

            // 寫 pgvector embeddings（upsert 語意：已存在則跳過）
            if (embeddings.Count > 0)
            {
                var existingIds = await db.Set<DocumentEmbedding>()
                    .Where(e => embeddings.Select(x => x.DocumentId).Contains(e.DocumentId))
                    .Select(e => e.DocumentId)
                    .ToHashSetAsync(ct);

                var newEmbeddings = embeddings.Where(e => !existingIds.Contains(e.DocumentId)).ToList();
                if (newEmbeddings.Count > 0)
                    db.Set<DocumentEmbedding>().AddRange(newEmbeddings);
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "ProcessingWorker: error during batch processing.");
        }
    }
}
