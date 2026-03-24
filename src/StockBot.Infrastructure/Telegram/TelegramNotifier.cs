using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockBot.Domain.Entities;
using StockBot.Domain.Enums;
using StockBot.Infrastructure.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace StockBot.Infrastructure.Telegram;

public sealed class TelegramNotifier(
    IOptions<TelegramOptions> options,
    ILogger<TelegramNotifier> logger) : ITelegramNotifier
{
    private readonly TelegramBotClient _bot    = new(options.Value.BotToken);
    private readonly TelegramOptions   _opts   = options.Value;

    public async Task SendAlertAsync(AlertSignal signal, CancellationToken ct = default)
    {
        var emoji = signal.Type switch
        {
            SignalType.Resonance         => "🚀",
            SignalType.BearishDivergence => "⚠️",
            SignalType.StealthStrength   => "🛡",
            SignalType.SectorRotation    => "🔄",
            _                            => "📊",
        };

        var text = $"""
            {emoji} *{signal.Summary}*

            股票代號：`{signal.StockCode}`
            觸發時間：{signal.TriggeredAt:yyyy-MM-dd HH:mm} UTC
            聲量變化：+{signal.MentionCountDelta:F1}%
            量比變化：{signal.VolumeDelta:+F1;-F1;0}%
            股價變化：{signal.PriceDelta:+F2;-F2;0}%
            """;

        try
        {
            await _bot.SendMessage(
                chatId:    _opts.ChatId,
                text:      text,
                parseMode: ParseMode.Markdown,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TelegramNotifier: failed to send alert for {Code}", signal.StockCode);
        }
    }

    public async Task SendConceptReviewAsync(DiscoveredConcept concept, CancellationToken ct = default)
    {
        var text = $"""
            🔍 *發現新概念詞彙審核*

            關鍵字：`{concept.Keyword}`
            出現次數：{concept.AppearanceCount}
            首次發現：{concept.FirstDiscoveredAt:yyyy-MM-dd HH:mm} UTC
            關聯股票：{concept.AssociatedStockCode ?? "—"}

            請回覆：
            `/approve {concept.Id}` 加入白名單
            `/reject {concept.Id}` 忽略
            """;

        try
        {
            await _bot.SendMessage(
                chatId:    _opts.ChatId,
                text:      text,
                parseMode: ParseMode.Markdown,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TelegramNotifier: failed to send concept review for id={Id}", concept.Id);
        }
    }
}
