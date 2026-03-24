using System.Text.Json.Serialization;

namespace StockBot.Infrastructure.MarketData;

/// <summary>
/// TWSE OpenAPI /v1/exchangeReport/STOCK_DAY_ALL 的原始回應欄位。
/// 所有數值欄位均為字串，部分可能為 "--"（該日未交易）。
/// 日期格式為民國年：YYYMMDD，例如 "1150323" = 2026/03/23。
/// </summary>
internal sealed class TwseStockDailyDto
{
    [JsonPropertyName("Date")]
    public string Date { get; init; } = string.Empty;

    [JsonPropertyName("Code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("TradeVolume")]
    public string TradeVolume { get; init; } = string.Empty;

    [JsonPropertyName("TradeValue")]
    public string TradeValue { get; init; } = string.Empty;

    [JsonPropertyName("OpeningPrice")]
    public string OpeningPrice { get; init; } = string.Empty;

    [JsonPropertyName("HighestPrice")]
    public string HighestPrice { get; init; } = string.Empty;

    [JsonPropertyName("LowestPrice")]
    public string LowestPrice { get; init; } = string.Empty;

    [JsonPropertyName("ClosingPrice")]
    public string ClosingPrice { get; init; } = string.Empty;

    [JsonPropertyName("Change")]
    public string Change { get; init; } = string.Empty;

    [JsonPropertyName("Transaction")]
    public string Transaction { get; init; } = string.Empty;
}
