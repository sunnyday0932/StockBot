using Microsoft.EntityFrameworkCore;
using StockBot.Infrastructure.Ai;
using StockBot.Infrastructure.Persistence;

namespace StockBot.Workers.Workers;

/// <summary>
/// 對已命中白名單的熱門文章執行 LLM 概念萃取（BottomUpProbe）：
///   1. 篩選：ProcessedAt IS NOT NULL AND EntityMatchCount > 0 AND ProbedAt IS NULL
///   2. 呼叫 ILlmConceptExtractor 萃取新關鍵字
///   3. 寫入 DiscoveredConcept（待使用者透過 Telegram /approve 審核）
/// </summary>
public sealed class BottomUpProbeWorker(
    ILlmConceptExtractor conceptExtractor,
    IServiceScopeFactory scopeFactory,
    ILogger<BottomUpProbeWorker> logger) : BackgroundService
{
    private const int BatchSize           = 10;  // LLM 費用較高，批次較小
    private const int PollIntervalSeconds = 120; // 每 2 分鐘掃一次
    private const int PttHotUpvoteThreshold = 10; // PTT 推文數達到此值才算熱門

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "BottomUpProbeWorker started. Interval: {Interval}s, batch: {Batch}.",
            PollIntervalSeconds, BatchSize);

        await ProbeBatchAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(PollIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ProbeBatchAsync(stoppingToken);
    }

    private async Task ProbeBatchAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StockBotDbContext>();

            // 篩選：已處理、有命中、尚未探針
            // 熱門條件：PTT 推文 ≥ 閾值，或非 PTT 來源（新聞本身即有參考價值）
            var docs = await db.SourceDocuments
                .Where(d => d.ProcessedAt != null
                         && d.EntityMatchCount > 0
                         && d.ProbedAt == null
                         && (d.PttUpvoteCount == null || d.PttUpvoteCount >= PttHotUpvoteThreshold))
                .OrderByDescending(d => d.PttUpvoteCount ?? 0)
                .ThenBy(d => d.PublishedAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (docs.Count == 0)
            {
                logger.LogDebug("BottomUpProbeWorker: no hot documents to probe.");
                return;
            }

            logger.LogInformation(
                "BottomUpProbeWorker: probing {Count} hot documents.", docs.Count);

            var now = DateTime.UtcNow;

            foreach (var doc in docs)
            {
                if (ct.IsCancellationRequested) break;

                var concepts = await conceptExtractor.ExtractConceptsAsync(
                    doc.Title, doc.Content, ct);

                doc.ProbedAt = now;

                if (concepts.Count == 0)
                {
                    logger.LogDebug(
                        "BottomUpProbeWorker: [{DocId}] no concepts extracted.", doc.DocumentId);
                    continue;
                }

                logger.LogInformation(
                    "BottomUpProbeWorker: [{DocId}] found {Count} concepts: {Concepts}",
                    doc.DocumentId, concepts.Count, string.Join(", ", concepts));

                foreach (var keyword in concepts)
                {
                    // upsert：已存在的 keyword 只更新 LastSeenAt + AppearanceCount
                    var existing = await db.DiscoveredConcepts
                        .FirstOrDefaultAsync(c => c.Keyword == keyword, ct);

                    if (existing is null)
                    {
                        db.DiscoveredConcepts.Add(new()
                        {
                            Keyword            = keyword,
                            SourceDocumentId   = doc.DocumentId,
                            FirstDiscoveredAt  = now,
                            LastSeenAt         = now,
                            AppearanceCount    = 1,
                            IsApprovedAndPromoted = false,
                        });
                    }
                    else
                    {
                        existing.LastSeenAt = now;
                        existing.AppearanceCount++;
                    }
                }
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "BottomUpProbeWorker: error during probe batch.");
        }
    }
}
