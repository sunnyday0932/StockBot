using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockBot.Domain.Enums;
using StockBot.Infrastructure.News;
using StockBot.Infrastructure.Options;
using StockBot.Infrastructure.Persistence;

namespace StockBot.Workers.Workers;

/// <summary>
/// 定時拉取鉅亨網財經新聞 RSS，解析後存入 PostgreSQL SourceDocuments。
/// 使用 HashSet 進行記憶體內去重，啟動時從 DB 載入近 7 天已知文章 ID。
/// </summary>
public sealed class CnyesNewsCrawlerWorker(
    HttpClient httpClient,
    IOptions<CnyesCrawlerOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<CnyesNewsCrawlerWorker> logger) : BackgroundService
{
    private readonly CnyesCrawlerOptions _options = options.Value;
    private readonly HashSet<string> _seenIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadRecentIdsFromDbAsync(stoppingToken);

        logger.LogInformation(
            "CnyesNewsCrawlerWorker started. RSS: {Url}, Interval: {Interval}s.",
            _options.RssUrl, _options.FetchIntervalSeconds);

        await CrawlAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.FetchIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await CrawlAsync(stoppingToken);
    }

    private async Task CrawlAsync(CancellationToken ct)
    {
        try
        {
            var xml      = await httpClient.GetStringAsync(_options.RssUrl, ct);
            var articles = CnyesRssParser.Parse(xml);
            var newDocs  = articles.Where(a => !_seenIds.Contains(a.DocumentId)).ToList();

            if (newDocs.Count == 0)
            {
                logger.LogDebug("CnyesNewsCrawlerWorker: no new articles.");
                return;
            }

            logger.LogInformation(
                "CnyesNewsCrawlerWorker: found {Count} new articles.", newDocs.Count);

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StockBotDbContext>();

            foreach (var doc in newDocs)
            {
                if (ct.IsCancellationRequested) break;

                if (!await db.SourceDocuments.AnyAsync(d => d.DocumentId == doc.DocumentId, ct))
                {
                    db.SourceDocuments.Add(doc);
                    logger.LogInformation(
                        "Cnyes [{Id}] saved: {Title}", doc.DocumentId, doc.Title);
                }

                _seenIds.Add(doc.DocumentId);
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "CnyesNewsCrawlerWorker encountered an error during crawl.");
        }
    }

    private async Task LoadRecentIdsFromDbAsync(CancellationToken ct)
    {
        try
        {
            var since = DateTime.UtcNow.AddDays(-7);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StockBotDbContext>();

            var ids = await db.SourceDocuments
                .Where(d => d.SourceType == SourceType.NewsCnyes && d.PublishedAt >= since)
                .Select(d => d.DocumentId)
                .ToListAsync(ct);

            foreach (var id in ids)
                _seenIds.Add(id);

            logger.LogInformation(
                "CnyesNewsCrawlerWorker: loaded {Count} recent article IDs from DB.", ids.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "CnyesNewsCrawlerWorker: could not load recent IDs from DB, starting fresh.");
        }
    }
}
