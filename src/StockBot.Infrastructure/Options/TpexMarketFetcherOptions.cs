namespace StockBot.Infrastructure.Options;

public sealed class TpexMarketFetcherOptions
{
    public string DailyCloseUrl { get; init; } =
        "https://www.tpex.org.tw/openapi/v1/tpex_mainboard_daily_close_quotes";
}
