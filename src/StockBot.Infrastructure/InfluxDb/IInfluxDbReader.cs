namespace StockBot.Infrastructure.InfluxDb;

public interface IInfluxDbReader
{
    /// <summary>
    /// 查詢指定時間範圍內，每支股票的 stock_mentions 總聲量。
    /// </summary>
    Task<Dictionary<string, long>> GetMentionCountsAsync(
        DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// 取得所有有資料股票的最新 OHLCV 快照（今日 vs 昨日收盤、近 5 日均量）。
    /// </summary>
    Task<Dictionary<string, OhlcvSnapshot>> GetOhlcvSnapshotsAsync(
        CancellationToken ct = default);
}

/// <summary>SignalAnalyzer 計算用的 OHLCV 快照。</summary>
public sealed record OhlcvSnapshot(
    decimal Close,
    decimal PrevClose,
    double  AvgVolume,
    long    LatestVolume);
