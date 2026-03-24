using StockBot.Domain.Enums;

namespace StockBot.Domain.Entities;

public class AlertSignal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime TriggeredAt { get; set; }
    public SignalType Type { get; set; }
    public string StockCode { get; set; } = string.Empty;
    public string StockName { get; set; } = string.Empty;

    // 觸發依據的量化數據快照
    public float MentionCountDelta { get; set; } // 過去 15 分鐘聲量變化率 (%)
    public float VolumeDelta { get; set; }        // 成交量相對均量變化率 (%)
    public float PriceDelta { get; set; }         // 價格變化率 (%)
    public float? SentimentAvg { get; set; }      // 近期平均情緒分數

    public string Summary { get; set; } = string.Empty; // 推播給使用者的摘要說明
}
