using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockBot.Infrastructure.Options;

namespace StockBot.Infrastructure.MarketData;

/// <summary>
/// 呼叫 TWSE OpenAPI 取得全上市股票當日 OHLCV 日線資料。
/// API URL 由 appsettings.json 的 TwseApi:StockDayAllUrl 設定。
/// </summary>
public sealed class TwseMarketFetcher(
    HttpClient httpClient,
    IOptions<TwseMarketFetcherOptions> options,
    ILogger<TwseMarketFetcher> logger)
    : IPollingMarketDataFetcher
{
    private readonly string _stockDayAllUrl = options.Value.StockDayAllUrl;

    public string SourceName => "TWSE";

    public async Task<IReadOnlyList<StockOhlcvRecord>> FetchAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Fetching TWSE daily OHLCV data...");

        var json = await httpClient.GetStringAsync(_stockDayAllUrl, ct);
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
            if (!MarketDataParser.TryParseDecimal(dto.OpeningPrice, out var open)   ||
                !MarketDataParser.TryParseDecimal(dto.HighestPrice, out var high)   ||
                !MarketDataParser.TryParseDecimal(dto.LowestPrice,  out var low)    ||
                !MarketDataParser.TryParseDecimal(dto.ClosingPrice, out var close)  ||
                !MarketDataParser.TryParseLong(dto.TradeVolume,     out var volume) ||
                !MarketDataParser.TryParseLong(dto.TradeValue,      out var turnover))
                continue;

            if (!MarketDataParser.TryParseRocDate(dto.Date, out var tradingDate))
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
                Market:        "TWSE"
            ));
        }

        return result;
    }
}
