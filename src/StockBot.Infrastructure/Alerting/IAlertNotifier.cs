using StockBot.Domain.Entities;

namespace StockBot.Infrastructure.Alerting;

public interface IAlertNotifier
{
    /// <summary>
    /// 推播 AlertSignal 到通知平台。
    /// </summary>
    Task SendAlertAsync(AlertSignal signal, CancellationToken ct = default);

    /// <summary>
    /// 推播 DiscoveredConcept 審核請求。
    /// </summary>
    Task SendConceptReviewAsync(DiscoveredConcept concept, CancellationToken ct = default);
}
