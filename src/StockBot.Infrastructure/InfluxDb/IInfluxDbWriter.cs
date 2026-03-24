using StockBot.Domain.Entities;
using StockBot.Infrastructure.MarketData;

namespace StockBot.Infrastructure.InfluxDb;

public interface IInfluxDbWriter
{
    Task WriteOhlcvAsync(IEnumerable<StockOhlcvRecord> records, CancellationToken ct = default);

    /// <summary>
    /// 將 TopDownMatcher 的比對結果寫入 InfluxDB stock_mentions。
    /// 每個 (document, entityMatch) 組合對應一個 DataPoint，timestamp 為文章發布時間。
    /// </summary>
    Task WriteMentionsAsync(
        IEnumerable<(SourceDocument Document, AnalysisResult Result)> items,
        CancellationToken ct = default);
}
