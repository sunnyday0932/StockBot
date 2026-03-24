using StockBot.Domain.Entities;

namespace StockBot.Infrastructure.Alerting;

public interface ISignalAnalyzer
{
    /// <summary>
    /// 分析當前量價聲量狀態，回傳觸發的訊號列表。
    /// </summary>
    Task<IReadOnlyList<AlertSignal>> AnalyzeAsync(CancellationToken ct = default);
}
