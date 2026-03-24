namespace StockBot.Infrastructure.MarketData;

/// <summary>
/// 定時拉取（Polling）模式的行情資料來源抽象。
/// 適用於 REST API 類型的資料源：TWSE、TPEX、FinMind 等。
///
/// 注意：WebSocket 串流型來源（如 Fugle）屬於不同執行典範，
/// 應實作獨立的 BackgroundService，不應實作此介面。
/// </summary>
public interface IPollingMarketDataFetcher
{
    /// <summary>來源識別名稱，用於 Log 與監控區分。</summary>
    string SourceName { get; }

    /// <summary>拉取當前最新 OHLCV 資料。</summary>
    Task<IReadOnlyList<StockOhlcvRecord>> FetchAsync(CancellationToken ct = default);
}
