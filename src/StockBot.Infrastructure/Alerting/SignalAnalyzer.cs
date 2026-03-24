using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockBot.Domain.Entities;
using StockBot.Domain.Enums;
using StockBot.Infrastructure.InfluxDb;
using StockBot.Infrastructure.Options;

namespace StockBot.Infrastructure.Alerting;

public sealed class SignalAnalyzer(
    IInfluxDbReader reader,
    IOptions<SignalAnalyzerOptions> options,
    ILogger<SignalAnalyzer> logger) : ISignalAnalyzer
{
    private readonly SignalAnalyzerOptions _opts = options.Value;

    public async Task<IReadOnlyList<AlertSignal>> AnalyzeAsync(CancellationToken ct = default)
    {
        var now     = DateTime.UtcNow;
        var window  = TimeSpan.FromMinutes(_opts.WindowMinutes);

        // 當前窗口 [now-window, now)
        var currFrom = now - window;
        // 前一個等長窗口 [now-2*window, now-window)
        var prevFrom = now - 2 * window;

        var currMentions = await reader.GetMentionCountsAsync(currFrom, now,     ct);
        var prevMentions = await reader.GetMentionCountsAsync(prevFrom, currFrom, ct);
        var snapshots    = await reader.GetOhlcvSnapshotsAsync(ct);

        var signals = new List<AlertSignal>();

        foreach (var (code, currCount) in currMentions)
        {
            if (currCount == 0) continue;

            var prevCount = prevMentions.GetValueOrDefault(code, 0L);

            // 聲量變化率（避免除以零）
            var mentionDelta = prevCount == 0
                ? 100f
                : (float)(currCount - prevCount) / prevCount * 100f;

            if (mentionDelta < _opts.MentionDeltaThreshold) continue;

            if (!snapshots.TryGetValue(code, out var snap)) continue;

            var priceDelta  = snap.PrevClose == 0m
                ? 0f
                : (float)((snap.Close - snap.PrevClose) / snap.PrevClose * 100m);

            var volumeDelta = snap.AvgVolume == 0.0
                ? 0f
                : (float)((snap.LatestVolume - snap.AvgVolume) / snap.AvgVolume * 100.0);

            AlertSignal? signal = null;

            // 共振：聲量激增 + 量比放大 + 股價上漲
            if (volumeDelta >= _opts.VolumeDeltaThreshold && priceDelta >= _opts.ResonancePriceDeltaMin)
            {
                signal = BuildSignal(SignalType.Resonance, code, mentionDelta, volumeDelta, priceDelta, now,
                    $"【共振】{code} 聲量+{mentionDelta:F1}% / 量比+{volumeDelta:F1}% / 股價+{priceDelta:F2}%");
            }
            // 背離出貨：聲量激增 + 股價不漲甚至下跌
            else if (priceDelta <= _opts.BearishPriceDeltaMax)
            {
                signal = BuildSignal(SignalType.BearishDivergence, code, mentionDelta, volumeDelta, priceDelta, now,
                    $"【背離】{code} 聲量+{mentionDelta:F1}% 但股價{priceDelta:F2}%，留意出貨風險");
            }

            if (signal is not null)
            {
                signals.Add(signal);
                logger.LogInformation("SignalAnalyzer: {Type} triggered for {Code}", signal.Type, code);
            }
        }

        return signals;
    }

    private static AlertSignal BuildSignal(
        SignalType type, string code,
        float mentionDelta, float volumeDelta, float priceDelta,
        DateTime triggeredAt, string summary) => new()
    {
        Type              = type,
        StockCode         = code,
        StockName         = code,   // TODO: 從 DB 查名稱
        MentionCountDelta = mentionDelta,
        VolumeDelta       = volumeDelta,
        PriceDelta        = priceDelta,
        TriggeredAt       = triggeredAt,
        Summary           = summary,
    };
}
