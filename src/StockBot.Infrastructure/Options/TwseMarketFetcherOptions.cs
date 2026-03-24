namespace StockBot.Infrastructure.Options;

public sealed class TwseMarketFetcherOptions
{
    public string StockDayAllUrl { get; init; } =
        "https://openapi.twse.com.tw/v1/exchangeReport/STOCK_DAY_ALL";
}
