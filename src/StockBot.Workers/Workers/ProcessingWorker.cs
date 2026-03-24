using Microsoft.EntityFrameworkCore;
using StockBot.Domain.Entities;
using StockBot.Infrastructure.InfluxDb;
using StockBot.Infrastructure.Persistence;
using StockBot.Infrastructure.Processing;

namespace StockBot.Workers.Workers;

/// <summary>
/// 定時掃描未處理的 SourceDocument（ProcessedAt IS NULL），
/// 執行 Aho-Corasick TopDownMatcher 比對，將 MentionCount 寫入 InfluxDB。
/// </summary>
public sealed class ProcessingWorker(
    ITopDownMatcher matcher,
    IInfluxDbWriter influxWriter,
    IServiceScopeFactory scopeFactory,
    ILogger<ProcessingWorker> logger) : BackgroundService
{
    private const int BatchSize          = 50;
    private const int PollIntervalSeconds = 30;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ProcessingWorker: building Aho-Corasick trie from whitelist...");
        await matcher.RebuildAsync(stoppingToken);
        logger.LogInformation("ProcessingWorker: trie ready, starting processing loop.");

        // 啟動後立即執行一次
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

            logger.LogInformation(
                "ProcessingWorker: processing {Count} documents.", docs.Count);

            var hits = new List<(SourceDocument Doc, AnalysisResult Result)>();

            foreach (var doc in docs)
            {
                if (ct.IsCancellationRequested) break;

                var result  = matcher.Match(doc);
                doc.ProcessedAt = DateTime.UtcNow;

                if (result.MatchedEntities.Count > 0)
                {
                    hits.Add((doc, result));
                    logger.LogInformation(
                        "Processing [{DocId}] → {Count} entities matched.",
                        doc.DocumentId, result.MatchedEntities.Count);
                }
            }

            // 先寫 InfluxDB，再 commit DB（失敗不影響 ProcessedAt 標記）
            if (hits.Count > 0)
                await influxWriter.WriteMentionsAsync(hits, ct);

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "ProcessingWorker: error during batch processing.");
        }
    }
}
