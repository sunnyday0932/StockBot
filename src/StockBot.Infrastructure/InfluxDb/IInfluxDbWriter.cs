using StockBot.Infrastructure.MarketData;

namespace StockBot.Infrastructure.InfluxDb;

public interface IInfluxDbWriter
{
    Task WriteOhlcvAsync(IEnumerable<StockOhlcvRecord> records, CancellationToken ct = default);
}
