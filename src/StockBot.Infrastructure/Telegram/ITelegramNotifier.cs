using StockBot.Domain.Entities;

namespace StockBot.Infrastructure.Telegram;

public interface ITelegramNotifier
{
    /// <summary>
    /// 推播 AlertSignal 到 Telegram 頻道。
    /// </summary>
    Task SendAlertAsync(AlertSignal signal, CancellationToken ct = default);

    /// <summary>
    /// 推播 DiscoveredConcept 審核請求（含 /approve ID / /reject ID 按鈕）。
    /// </summary>
    Task SendConceptReviewAsync(DiscoveredConcept concept, CancellationToken ct = default);
}
