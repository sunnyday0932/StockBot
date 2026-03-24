using InfluxDB.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockBot.Infrastructure.Options;

namespace StockBot.Infrastructure.InfluxDb;

public sealed class InfluxDbReader(
    IOptions<InfluxDbOptions> options,
    ILogger<InfluxDbReader> logger) : IInfluxDbReader, IDisposable
{
    private readonly InfluxDBClient   _client  = new(options.Value.Url, options.Value.Token);
    private readonly InfluxDbOptions  _options = options.Value;

    /// <summary>
    /// 查詢 [from, to) 區間內每支股票的 stock_mentions 總聲量（MentionCount sum）。
    /// </summary>
    public async Task<Dictionary<string, long>> GetMentionCountsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        var flux = $"""
            from(bucket: "{_options.Bucket}")
              |> range(start: {from:o}, stop: {to:o})
              |> filter(fn: (r) => r["_measurement"] == "stock_mentions")
              |> filter(fn: (r) => r["_field"] == "MentionCount")
              |> group(columns: ["StockCode"])
              |> sum()
            """;

        var result = new Dictionary<string, long>();

        try
        {
            var tables = await _client.GetQueryApi().QueryAsync(flux, _options.Org, ct);
            foreach (var table in tables)
            foreach (var record in table.Records)
            {
                var code = record.GetValueByKey("StockCode")?.ToString();
                if (!string.IsNullOrEmpty(code))
                    result[code] = Convert.ToInt64(record.GetValue());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "InfluxDbReader: failed to query mention counts.");
        }

        return result;
    }

    /// <summary>
    /// 查詢所有股票的最新兩日收盤價與近 5 日平均成交量。
    /// </summary>
    public async Task<Dictionary<string, OhlcvSnapshot>> GetOhlcvSnapshotsAsync(
        CancellationToken ct = default)
    {
        // 最新收盤價
        var latestClose  = await QueryLastFieldAsync("stock_ohlcv", "Close",  lookbackDays: 10, ct);
        // 前一日收盤價（取倒數第二筆）
        var prevClose    = await QueryPrevFieldAsync("stock_ohlcv", "Close",  lookbackDays: 10, ct);
        // 最新成交量
        var latestVolume = await QueryLastFieldAsync("stock_ohlcv", "Volume", lookbackDays: 10, ct);
        // 近 5 日平均成交量
        var avgVolume    = await QueryMeanFieldAsync("stock_ohlcv", "Volume", lookbackDays: 6, ct);

        var snapshots = new Dictionary<string, OhlcvSnapshot>();

        foreach (var code in latestClose.Keys)
        {
            if (!prevClose.TryGetValue(code, out var prev)) continue;

            snapshots[code] = new OhlcvSnapshot(
                Close:        (decimal)Convert.ToDouble(latestClose[code]),
                PrevClose:    (decimal)Convert.ToDouble(prev),
                AvgVolume:    Convert.ToDouble(avgVolume.GetValueOrDefault(code, 0.0)),
                LatestVolume: Convert.ToInt64(latestVolume.GetValueOrDefault(code, 0.0)));
        }

        return snapshots;
    }

    // ── 私有 Flux 輔助方法 ──────────────────────────────────────────

    private async Task<Dictionary<string, object>> QueryLastFieldAsync(
        string measurement, string field, int lookbackDays, CancellationToken ct)
    {
        var flux = $"""
            from(bucket: "{_options.Bucket}")
              |> range(start: -{lookbackDays}d)
              |> filter(fn: (r) => r["_measurement"] == "{measurement}" and r["_field"] == "{field}")
              |> group(columns: ["StockCode"])
              |> last()
            """;
        return await ExecuteScalarQueryAsync(flux, "StockCode", ct);
    }

    private async Task<Dictionary<string, object>> QueryPrevFieldAsync(
        string measurement, string field, int lookbackDays, CancellationToken ct)
    {
        // 取所有值，排序後取倒數第二筆
        var flux = $"""
            from(bucket: "{_options.Bucket}")
              |> range(start: -{lookbackDays}d)
              |> filter(fn: (r) => r["_measurement"] == "{measurement}" and r["_field"] == "{field}")
              |> group(columns: ["StockCode"])
              |> sort(columns: ["_time"], desc: false)
              |> tail(n: 2)
              |> first()
            """;
        return await ExecuteScalarQueryAsync(flux, "StockCode", ct);
    }

    private async Task<Dictionary<string, object>> QueryMeanFieldAsync(
        string measurement, string field, int lookbackDays, CancellationToken ct)
    {
        var flux = $"""
            from(bucket: "{_options.Bucket}")
              |> range(start: -{lookbackDays}d)
              |> filter(fn: (r) => r["_measurement"] == "{measurement}" and r["_field"] == "{field}")
              |> group(columns: ["StockCode"])
              |> mean()
            """;
        return await ExecuteScalarQueryAsync(flux, "StockCode", ct);
    }

    private async Task<Dictionary<string, object>> ExecuteScalarQueryAsync(
        string flux, string tagKey, CancellationToken ct)
    {
        var result = new Dictionary<string, object>();
        try
        {
            var tables = await _client.GetQueryApi().QueryAsync(flux, _options.Org, ct);
            foreach (var table in tables)
            foreach (var record in table.Records)
            {
                var key = record.GetValueByKey(tagKey)?.ToString();
                var val = record.GetValue();
                if (!string.IsNullOrEmpty(key) && val is not null)
                    result[key] = val;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "InfluxDbReader: query failed for flux: {Flux}", flux[..Math.Min(80, flux.Length)]);
        }
        return result;
    }

    public void Dispose() => _client.Dispose();
}
