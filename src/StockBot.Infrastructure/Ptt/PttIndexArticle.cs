namespace StockBot.Infrastructure.Ptt;

internal sealed record PttIndexArticle(
    string ArticleId,
    string Title,
    string Author,
    string Href);
