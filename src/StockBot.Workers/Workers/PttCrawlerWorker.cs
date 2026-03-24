using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockBot.Domain.Enums;
using StockBot.Infrastructure.Persistence;
using StockBot.Infrastructure.Options;
using StockBot.Infrastructure.Ptt;

namespace StockBot.Workers.Workers;

/// <summary>
/// 定時拉取 PTT 股板最新文章，解析後存入 PostgreSQL SourceDocuments。
/// 使用 HashSet 進行記憶體內去重，啟動時從 DB 載入近 7 天已爬文章 ID。
/// </summary>
public sealed class PttCrawlerWorker(
    HttpClient httpClient,
    IOptions<PttCrawlerOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<PttCrawlerWorker> logger) : BackgroundService
{
    private readonly PttCrawlerOptions _options = options.Value;
    private readonly HashSet<string> _seenIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadRecentIdsFromDbAsync(stoppingToken);

        logger.LogInformation(
            "PttCrawlerWorker started. Board: {Board}, Interval: {Interval}s.",
            _options.Board, _options.FetchIntervalSeconds);

        // 啟動後立刻執行一次
        await CrawlAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.FetchIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await CrawlAsync(stoppingToken);
    }

    private async Task CrawlAsync(CancellationToken ct)
    {
        try
        {
            var indexUrl = $"{_options.BaseUrl}/bbs/{_options.Board}/index.html";
            var indexHtml = await httpClient.GetStringAsync(indexUrl, ct);
            var articles = PttWebParser.ParseIndex(indexHtml);

            var newArticles = articles.Where(a => !_seenIds.Contains(a.ArticleId)).ToList();

            if (newArticles.Count == 0)
            {
                logger.LogDebug("PttCrawlerWorker: no new articles.");
                return;
            }

            logger.LogInformation(
                "PttCrawlerWorker: found {Count} new articles.", newArticles.Count);

            foreach (var article in newArticles)
            {
                if (ct.IsCancellationRequested) break;
                await ProcessArticleAsync(article, ct);

                // 避免對 PTT 請求過於頻繁
                await Task.Delay(_options.ArticleFetchDelayMs, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "PttCrawlerWorker encountered an error during crawl.");
        }
    }

    private async Task ProcessArticleAsync(PttIndexArticle article, CancellationToken ct)
    {
        try
        {
            var articleUrl  = $"{_options.BaseUrl}{article.Href}";
            var articleHtml = await httpClient.GetStringAsync(articleUrl, ct);
            var doc         = PttWebParser.ParseArticle(articleHtml, article.ArticleId, articleUrl);

            if (doc is null)
            {
                logger.LogWarning("PttCrawlerWorker: failed to parse article {Id}.", article.ArticleId);
                _seenIds.Add(article.ArticleId); // 避免重複嘗試
                return;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StockBotDbContext>();

            // 使用 Upsert 語意，已存在就跳過
            if (!await db.SourceDocuments.AnyAsync(d => d.DocumentId == doc.DocumentId, ct))
            {
                db.SourceDocuments.Add(doc);
                await db.SaveChangesAsync(ct);
                logger.LogInformation(
                    "PTT [{Id}] saved: {Title} (推{Up}/噓{Down}/→{Arrow})",
                    doc.DocumentId, doc.Title,
                    doc.PttUpvoteCount, doc.PttDownvoteCount, doc.PttArrowCount);
            }

            _seenIds.Add(article.ArticleId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "PttCrawlerWorker: error processing article {Id}.", article.ArticleId);
        }
    }

    /// <summary>啟動時從 DB 載入近 7 天已知文章 ID，避免重複儲存。</summary>
    private async Task LoadRecentIdsFromDbAsync(CancellationToken ct)
    {
        try
        {
            var since = DateTime.UtcNow.AddDays(-7);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StockBotDbContext>();

            var ids = await db.SourceDocuments
                .Where(d => d.SourceType == SourceType.PttStock && d.PublishedAt >= since)
                .Select(d => d.DocumentId)
                .ToListAsync(ct);

            foreach (var id in ids)
                _seenIds.Add(id);

            logger.LogInformation(
                "PttCrawlerWorker: loaded {Count} recent article IDs from DB.", ids.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PttCrawlerWorker: could not load recent IDs from DB, starting fresh.");
        }
    }
}
