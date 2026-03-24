namespace StockBot.Infrastructure.Ai;

public interface ILlmConceptExtractor
{
    /// <summary>
    /// 從文章標題與內文中，萃取出潛在的新概念關鍵字（如「玻璃基板」、「CoWoS」）。
    /// 回傳的關鍵字尚未審核，需透過 Telegram /approve 升級為正式 TrackedEntity。
    /// </summary>
    Task<IReadOnlyList<string>> ExtractConceptsAsync(
        string title,
        string content,
        CancellationToken ct = default);
}
