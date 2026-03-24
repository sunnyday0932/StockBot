namespace StockBot.Infrastructure.Options;

public sealed class CnyesCrawlerOptions
{
    public string RssUrl               { get; init; } = "https://www.cnyes.com/rss/cat/tw_stock_news";
    public int    FetchIntervalSeconds { get; init; } = 60;
}
