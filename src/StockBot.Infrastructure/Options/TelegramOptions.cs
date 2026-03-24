namespace StockBot.Infrastructure.Options;

public sealed class TelegramOptions
{
    /// <summary>Bot Token，從 @BotFather 取得。</summary>
    public string BotToken { get; init; } = string.Empty;

    /// <summary>推播目標的 Chat ID（頻道用負數，群組同）。</summary>
    public long ChatId { get; init; }

    /// <summary>Long-polling timeout（秒）。</summary>
    public int PollingTimeoutSeconds { get; init; } = 30;
}
