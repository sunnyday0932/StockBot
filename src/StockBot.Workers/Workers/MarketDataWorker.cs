using StockBot.Infrastructure.InfluxDb;
using StockBot.Infrastructure.MarketData;

namespace StockBot.Workers.Workers;

/// <summary>
/// 定時從 TWSE API 拉取日線 OHLCV 資料並寫入 InfluxDB。
/// 執行頻率由 appsettings.json 的 MarketData:FetchIntervalMinutes 控制。
/// </summary>
public sealed class MarketDataWorker(
    TwseMarketFetcher fetcher,
    IInfluxDbWriter influxWriter,
    IConfiguration configuration,
    ILogger<MarketDataWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = configuration.GetValue<int>("MarketData:FetchIntervalMinutes", 5);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        logger.LogInformation(
            "MarketDataWorker started. Fetch interval: {Interval} minutes.", intervalMinutes);

        // 啟動後立刻執行一次，不等第一個 interval
        await FetchAndWriteAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await FetchAndWriteAsync(stoppingToken);
        }
    }

    private async Task FetchAndWriteAsync(CancellationToken ct)
    {
        try
        {
            var records = await fetcher.FetchAsync(ct);
            await influxWriter.WriteOhlcvAsync(records, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 單次失敗不中斷 Worker，等下個 interval 重試
            logger.LogError(ex, "MarketDataWorker encountered an error during fetch/write.");
        }
    }
}
