using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace StockBot.Infrastructure.MarketData;

/// <summary>
/// 呼叫 TWSE OpenAPI 取得全上市股票當日 OHLCV 日線資料。
/// API：GET https://openapi.twse.com.tw/v1/exchangeReport/STOCK_DAY_ALL
/// </summary>
public sealed class TwseMarketFetcher(HttpClient httpClient, ILogger<TwseMarketFetcher> logger)
{
    private const string StockDayAllUrl =
        "https://openapi.twse.com.tw/v1/exchangeReport/STOCK_DAY_ALL";

    public async Task<IReadOnlyList<StockOhlcvRecord>> FetchAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Fetching TWSE daily OHLCV data...");

        var json = await httpClient.GetStringAsync(StockDayAllUrl, ct);
        var records = Parse(json);

        logger.LogInformation("Fetched {Count} TWSE records.", records.Count);
        return records;
    }

    /// <summary>
    /// 解析 TWSE API JSON 回應為 StockOhlcvRecord 列表。
    /// internal 以便單元測試直接呼叫，不需要真實 HTTP。
    /// </summary>
    internal static IReadOnlyList<StockOhlcvRecord> Parse(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<TwseStockDailyDto>>(json);
        if (dtos is null or { Count: 0 })
            return [];

        var result = new List<StockOhlcvRecord>(dtos.Count);

        foreach (var dto in dtos)
        {
            // 跳過數值欄位為 "--" 的停牌或異常股票
            if (!TryParseDecimal(dto.OpeningPrice, out var open)  ||
                !TryParseDecimal(dto.HighestPrice, out var high)  ||
                !TryParseDecimal(dto.LowestPrice,  out var low)   ||
                !TryParseDecimal(dto.ClosingPrice, out var close) ||
                !TryParseLong(dto.TradeVolume,    out var volume) ||
                !TryParseLong(dto.TradeValue,     out var turnover))
            {
                continue;
            }

            if (!TryParseRocDate(dto.Date, out var tradingDate))
                continue;

            result.Add(new StockOhlcvRecord(
                StockCode:    dto.Code,
                StockName:    dto.Name,
                TradingDate:  tradingDate,
                Open:         open,
                High:         high,
                Low:          low,
                Close:        close,
                Volume:       volume,
                TurnoverValue: turnover,
                Market:       "TWSE"
            ));
        }

        return result;
    }

    /// <summary>
    /// 民國年格式 YYYMMDD → DateOnly。
    /// 例：「1150323」→ DateOnly(2026, 3, 23)
    /// </summary>
    internal static bool TryParseRocDate(string rocDate, out DateOnly result)
    {
        result = default;

        if (rocDate.Length != 7)
            return false;

        if (!int.TryParse(rocDate[..3], out var rocYear) ||
            !int.TryParse(rocDate[3..5], out var month)  ||
            !int.TryParse(rocDate[5..], out var day))
            return false;

        var gregorianYear = rocYear + 1911;

        try
        {
            result = new DateOnly(gregorianYear, month, day);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseDecimal(string raw, out decimal value)
    {
        // 移除千分位逗號後解析
        var cleaned = raw.Replace(",", "");
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseLong(string raw, out long value)
    {
        var cleaned = raw.Replace(",", "");
        return long.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
