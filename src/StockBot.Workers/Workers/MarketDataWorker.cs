using StockBot.Infrastructure.InfluxDb;
using StockBot.Infrastructure.MarketData;

namespace StockBot.Workers.Workers;

/// <summary>
/// 定時從多個 REST API 來源拉取日線 OHLCV 資料並寫入 InfluxDB。
/// 執行頻率由 appsettings.json 的 MarketData:FetchIntervalMinutes 控制。
/// 新增資料來源只需實作 IPollingMarketDataFetcher 並在 DI 容器中註冊即可。
/// </summary>
public sealed class MarketDataWorker(
    IEnumerable<IPollingMarketDataFetcher> fetchers,
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
        await FetchAndWriteAllAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await FetchAndWriteAllAsync(stoppingToken);
        }
    }

    private async Task FetchAndWriteAllAsync(CancellationToken ct)
    {
        foreach (var fetcher in fetchers)
        {
            try
            {
                var records = await fetcher.FetchAsync(ct);
                await influxWriter.WriteOhlcvAsync(records, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 單一來源失敗不中斷其他來源，等下個 interval 重試
                logger.LogError(ex,
                    "MarketDataWorker encountered an error fetching from {Source}.", fetcher.SourceName);
            }
        }
    }
}
