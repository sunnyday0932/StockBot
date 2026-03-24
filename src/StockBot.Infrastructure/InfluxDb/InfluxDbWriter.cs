using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockBot.Infrastructure.MarketData;
using StockBot.Infrastructure.Options;

namespace StockBot.Infrastructure.InfluxDb;

public sealed class InfluxDbWriter : IInfluxDbWriter, IDisposable
{
    private readonly InfluxDBClient _client;
    private readonly InfluxDbOptions _options;
    private readonly ILogger<InfluxDbWriter> _logger;

    public InfluxDbWriter(IOptions<InfluxDbOptions> options, ILogger<InfluxDbWriter> logger)
    {
        _options = options.Value;
        _logger  = logger;
        _client  = new InfluxDBClient(_options.Url, _options.Token);
    }

    public async Task WriteOhlcvAsync(IEnumerable<StockOhlcvRecord> records, CancellationToken ct = default)
    {
        var points = records.Select(ToPoint).ToList();

        if (points.Count == 0)
            return;

        var writeApi = _client.GetWriteApiAsync();
        await writeApi.WritePointsAsync(points, _options.Bucket, _options.Org, ct);

        _logger.LogInformation("Wrote {Count} OHLCV points to InfluxDB.", points.Count);
    }

    private static PointData ToPoint(StockOhlcvRecord r)
    {
        // 將 DateOnly 轉為當日台灣收盤時間（UTC）：台灣時區 UTC+8，收盤 13:30 = UTC 05:30
        var timestamp = r.TradingDate.ToDateTime(new TimeOnly(5, 30), DateTimeKind.Utc);

        return PointData.Measurement("stock_ohlcv")
            .Tag("StockCode",  r.StockCode)
            .Tag("Market",     r.Market)
            .Tag("DataSource", r.Market == "TWSE" ? "TwseApi" : "TpexApi")
            .Field("Open",          (double)r.Open)
            .Field("High",          (double)r.High)
            .Field("Low",           (double)r.Low)
            .Field("Close",         (double)r.Close)
            .Field("Volume",        r.Volume)
            .Field("TurnoverValue", (double)r.TurnoverValue)
            .Timestamp(timestamp, WritePrecision.S);
    }

    public void Dispose() => _client.Dispose();
}
