using StockBot.Domain.Entities;
using StockBot.Domain.Enums;

namespace StockBot.Tests.Unit.Entities;

public class AlertSignalTests
{
    [Fact]
    public void Alert_signal_should_generate_unique_id_by_default()
    {
        var signal1 = new AlertSignal();
        var signal2 = new AlertSignal();

        Assert.NotEqual(signal1.Id, signal2.Id);
    }

    [Theory]
    [InlineData(SignalType.Resonance)]
    [InlineData(SignalType.BearishDivergence)]
    [InlineData(SignalType.StealthStrength)]
    [InlineData(SignalType.SectorRotation)]
    public void All_signal_types_are_assignable(SignalType type)
    {
        var signal = new AlertSignal
        {
            Type = type,
            StockCode = "2330",
            StockName = "台積電",
            TriggeredAt = DateTime.UtcNow,
            MentionCountDelta = 150f,
            VolumeDelta = 80f,
            PriceDelta = 2.5f,
            Summary = "測試訊號"
        };

        Assert.Equal(type, signal.Type);
    }

    [Fact]
    public void Sentiment_avg_is_optional()
    {
        var signal = new AlertSignal { StockCode = "2330", StockName = "台積電" };

        Assert.Null(signal.SentimentAvg);
    }
}
