using System.Text.Json.Serialization;

namespace StockBot.Infrastructure.MarketData;

internal sealed class TpexStockDailyDto
{
    [JsonPropertyName("Date")]                   public string Date              { get; init; } = "";
    [JsonPropertyName("SecuritiesCompanyCode")]  public string Code              { get; init; } = "";
    [JsonPropertyName("CompanyName")]            public string Name              { get; init; } = "";
    [JsonPropertyName("Open")]                   public string Open              { get; init; } = "";
    [JsonPropertyName("High")]                   public string High              { get; init; } = "";
    [JsonPropertyName("Low")]                    public string Low               { get; init; } = "";
    [JsonPropertyName("Close")]                  public string Close             { get; init; } = "";
    [JsonPropertyName("TradingShares")]          public string TradingShares     { get; init; } = "";
    [JsonPropertyName("TransactionAmount")]      public string TransactionAmount { get; init; } = "";
}
