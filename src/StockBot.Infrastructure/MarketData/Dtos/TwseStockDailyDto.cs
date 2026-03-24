using System.Text.Json.Serialization;

namespace StockBot.Infrastructure.MarketData;

internal sealed class TwseStockDailyDto
{
    [JsonPropertyName("Date")]          public string Date         { get; init; } = "";
    [JsonPropertyName("Code")]          public string Code         { get; init; } = "";
    [JsonPropertyName("Name")]          public string Name         { get; init; } = "";
    [JsonPropertyName("OpeningPrice")]  public string OpeningPrice { get; init; } = "";
    [JsonPropertyName("HighestPrice")]  public string HighestPrice { get; init; } = "";
    [JsonPropertyName("LowestPrice")]   public string LowestPrice  { get; init; } = "";
    [JsonPropertyName("ClosingPrice")]  public string ClosingPrice { get; init; } = "";
    [JsonPropertyName("TradeVolume")]   public string TradeVolume  { get; init; } = "";
    [JsonPropertyName("TradeValue")]    public string TradeValue   { get; init; } = "";
}
