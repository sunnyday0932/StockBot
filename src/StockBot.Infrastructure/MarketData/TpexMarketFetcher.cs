using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StockBot.Infrastructure.MarketData;

/// <summary>
/// 呼叫 TPEX OpenAPI 取得全上櫃股票當日 OHLCV 日線資料。
/// API URL 由 appsettings.json 的 TpexApi:DailyCloseUrl 設定。
/// </summary>
public sealed class TpexMarketFetcher(
    HttpClient httpClient,
    IOptions<TpexMarketFetcherOptions> options,
    ILogger<TpexMarketFetcher> logger)
    : IPollingMarketDataFetcher
{
    private readonly string _dailyCloseUrl = options.Value.DailyCloseUrl;

    public string SourceName => "TPEX";

    public async Task<IReadOnlyList<StockOhlcvRecord>> FetchAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Fetching TPEX daily OHLCV data...");

        var json = await httpClient.GetStringAsync(_dailyCloseUrl, ct);
        var records = Parse(json);

        logger.LogInformation("Fetched {Count} TPEX records.", records.Count);
        return records;
    }

    /// <summary>
    /// 解析 TPEX API JSON 回應為 StockOhlcvRecord 列表。
    /// internal 以便單元測試直接呼叫，不需要真實 HTTP。
    /// </summary>
    internal static IReadOnlyList<StockOhlcvRecord> Parse(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<TpexStockDailyDto>>(json);
        if (dtos is null or { Count: 0 })
            return [];

        var result = new List<StockOhlcvRecord>(dtos.Count);

        foreach (var dto in dtos)
        {
            // 跳過數值欄位為 "---" 的停牌或異常股票
            if (!TryParseDecimal(dto.Open,              out var open)    ||
                !TryParseDecimal(dto.High,              out var high)    ||
                !TryParseDecimal(dto.Low,               out var low)     ||
                !TryParseDecimal(dto.Close,             out var close)   ||
                !TryParseLong(dto.TradingShares,        out var volume)  ||
                !TryParseLong(dto.TransactionAmount,    out var turnover))
            {
                continue;
            }

            if (!TryParseRocDate(dto.Date, out var tradingDate))
                continue;

            result.Add(new StockOhlcvRecord(
                StockCode:     dto.Code,
                StockName:     dto.Name,
                TradingDate:   tradingDate,
                Open:          open,
                High:          high,
                Low:           low,
                Close:         close,
                Volume:        volume,
                TurnoverValue: turnover,
                Market:        "TPEX"
            ));
        }

        return result;
    }

    /// <summary>民國年格式 YYYMMDD → DateOnly。例：「1150323」→ DateOnly(2026, 3, 23)</summary>
    internal static bool TryParseRocDate(string rocDate, out DateOnly result)
    {
        result = default;

        if (rocDate.Length != 7)
            return false;

        if (!int.TryParse(rocDate[..3], out var rocYear) ||
            !int.TryParse(rocDate[3..5], out var month)  ||
            !int.TryParse(rocDate[5..],  out var day))
            return false;

        try
        {
            result = new DateOnly(rocYear + 1911, month, day);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseDecimal(string raw, out decimal value)
    {
        var cleaned = raw.Replace(",", "");
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseLong(string raw, out long value)
    {
        var cleaned = raw.Replace(",", "");
        return long.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
