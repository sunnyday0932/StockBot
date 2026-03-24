using Microsoft.EntityFrameworkCore;
using StockBot.Domain.Entities;
using StockBot.Domain.Enums;
using StockBot.Infrastructure.MarketData;
using StockBot.Infrastructure.Persistence;

namespace StockBot.Workers.Workers;

/// <summary>
/// 啟動時從 TWSE / TPEX 拉取完整股票清單，同步寫入 PostgreSQL TrackedEntity 白名單。
/// 每個股票自動建立兩個 EntityAlias：股票代號（用於精確比對）與公司名稱（用於文字比對）。
/// 採 idempotent upsert：已存在的股票代號直接跳過，確保重複啟動安全。
/// </summary>
public sealed class WhitelistInitializerWorker(
    IEnumerable<IPollingMarketDataFetcher> fetchers,
    IServiceScopeFactory scopeFactory,
    ILogger<WhitelistInitializerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WhitelistInitializerWorker: starting stock whitelist sync...");

        var allRecords = new List<StockOhlcvRecord>();

        foreach (var fetcher in fetchers)
        {
            try
            {
                var records = await fetcher.FetchAsync(stoppingToken);
                allRecords.AddRange(records);
                logger.LogInformation(
                    "WhitelistInitializerWorker: fetched {Count} stocks from {Source}.",
                    records.Count, fetcher.SourceName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "WhitelistInitializerWorker: failed to fetch from {Source}.", fetcher.SourceName);
            }
        }

        if (allRecords.Count == 0)
        {
            logger.LogWarning("WhitelistInitializerWorker: no records fetched, skipping sync.");
            return;
        }

        await UpsertTrackedEntitiesAsync(allRecords, stoppingToken);
    }

    private async Task UpsertTrackedEntitiesAsync(
        List<StockOhlcvRecord> records, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StockBotDbContext>();

        // 一次載入所有已知股票代號，避免每筆都查 DB
        var existingCodes = await db.TrackedEntities
            .Where(e => e.StockCode != null)
            .Select(e => e.StockCode!)
            .ToHashSetAsync(ct);

        var toAdd = new List<TrackedEntity>();

        foreach (var r in records)
        {
            // 已存在或本批次重複（TWSE / TPEX 不會有重疊，但保險起見）
            if (!existingCodes.Add(r.StockCode))
                continue;

            toAdd.Add(new TrackedEntity
            {
                Type        = EntityType.Stock,
                PrimaryName = r.StockName,
                StockCode   = r.StockCode,
                Aliases     =
                [
                    new EntityAlias { Keyword = r.StockCode  }, // 代號比對：「2330」
                    new EntityAlias { Keyword = r.StockName  }, // 名稱比對：「台積電」
                ]
            });
        }

        if (toAdd.Count > 0)
        {
            db.TrackedEntities.AddRange(toAdd);
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "WhitelistInitializerWorker: sync complete. Added {Added} new, skipped {Skipped} existing.",
            toAdd.Count, records.Count - toAdd.Count);
    }
}
