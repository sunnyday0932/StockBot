namespace StockBot.Infrastructure.Options;

public sealed class SignalAnalyzerOptions
{
    /// <summary>分析窗口（分鐘），與前一個等長窗口比較聲量變化。</summary>
    public int WindowMinutes { get; init; } = 15;

    /// <summary>觸發共振 / 背離所需的最低聲量變化率（%）。</summary>
    public float MentionDeltaThreshold { get; init; } = 50f;

    /// <summary>共振判斷：成交量超過均量的比例（%）。</summary>
    public float VolumeDeltaThreshold { get; init; } = 50f;

    /// <summary>共振判斷：股價上漲幅度門檻（%）。</summary>
    public float ResonancePriceDeltaMin { get; init; } = 2f;

    /// <summary>背離判斷：股價下跌幅度門檻（%，負值）。</summary>
    public float BearishPriceDeltaMax { get; init; } = -1f;

    /// <summary>每輪分析的執行間隔（秒）。</summary>
    public int AnalysisIntervalSeconds { get; init; } = 900; // 15 分鐘
}
