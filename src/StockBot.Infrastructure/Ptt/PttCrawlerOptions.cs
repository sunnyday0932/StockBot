namespace StockBot.Infrastructure.Ptt;

public sealed class PttCrawlerOptions
{
    public string BaseUrl            { get; init; } = "https://www.ptt.cc";
    public string Board              { get; init; } = "Stock";
    public int    FetchIntervalSeconds { get; init; } = 60;
    public int    ArticleFetchDelayMs  { get; init; } = 500;
}
