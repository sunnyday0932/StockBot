using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockBot.Domain.Entities;
using StockBot.Infrastructure.Options;
using StockBot.Infrastructure.Persistence;
using StockBot.Infrastructure.Telegram;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace StockBot.Workers.Workers;

/// <summary>
/// Long-polling worker：接收 Telegram 指令（/approve、/reject）並處理 DiscoveredConcept 審核。
/// 同時每輪分析後推播待審核的新概念。
/// </summary>
public sealed class TelegramBotWorker(
    IServiceScopeFactory scopeFactory,
    ITelegramNotifier notifier,
    IOptions<TelegramOptions> options,
    ILogger<TelegramBotWorker> logger) : BackgroundService
{
    private readonly TelegramBotClient _bot  = new(options.Value.BotToken);
    private readonly TelegramOptions   _opts = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TelegramBotWorker started");

        // 啟動 long-polling（在背景接收 Update）
        _bot.StartReceiving(
            updateHandler:      HandleUpdateAsync,
            errorHandler:       HandleErrorAsync,
            receiverOptions:    new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message],
                DropPendingUpdates = true,
            },
            cancellationToken: stoppingToken);

        // 定期推播未審核的 DiscoveredConcept（每 30 分鐘）
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PushPendingConceptsAsync(stoppingToken);
        }
    }

    // ── Telegram update handler ──────────────────────────────────────

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message?.Text is not { } text) return;

        var parts = text.Trim().Split(' ', 2);
        var cmd   = parts[0].ToLowerInvariant();
        var arg   = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        switch (cmd)
        {
            case "/approve":
                await HandleApproveAsync(arg, update.Message.Chat.Id, ct);
                break;
            case "/reject":
                await HandleRejectAsync(arg, update.Message.Chat.Id, ct);
                break;
        }
    }

    private async Task HandleApproveAsync(string arg, long chatId, CancellationToken ct)
    {
        if (!int.TryParse(arg, out var id))
        {
            await _bot.SendMessage(chatId, "用法：/approve <id>", cancellationToken: ct);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StockBotDbContext>();

        var concept = await db.Set<DiscoveredConcept>().FindAsync([id], ct);
        if (concept is null)
        {
            await _bot.SendMessage(chatId, $"找不到 concept id={id}", cancellationToken: ct);
            return;
        }

        if (!concept.IsApprovedAndPromoted)
        {
            // 升級為 TrackedEntity（Concept 類型）
            var entity = new TrackedEntity
            {
                Type        = Domain.Enums.EntityType.Concept,
                PrimaryName = concept.Keyword,
            };
            entity.Aliases.Add(new EntityAlias { Keyword = concept.Keyword });

            db.Set<TrackedEntity>().Add(entity);
            concept.IsApprovedAndPromoted = true;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("TelegramBotWorker: concept '{Keyword}' approved and promoted", concept.Keyword);
            await _bot.SendMessage(chatId, $"✅ 已將「{concept.Keyword}」加入白名單", cancellationToken: ct);
        }
        else
        {
            await _bot.SendMessage(chatId, $"「{concept.Keyword}」已經升級過了", cancellationToken: ct);
        }
    }

    private async Task HandleRejectAsync(string arg, long chatId, CancellationToken ct)
    {
        if (!int.TryParse(arg, out var id))
        {
            await _bot.SendMessage(chatId, "用法：/reject <id>", cancellationToken: ct);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StockBotDbContext>();

        var concept = await db.Set<DiscoveredConcept>().FindAsync([id], ct);
        if (concept is null)
        {
            await _bot.SendMessage(chatId, $"找不到 concept id={id}", cancellationToken: ct);
            return;
        }

        db.Set<DiscoveredConcept>().Remove(concept);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("TelegramBotWorker: concept '{Keyword}' rejected and removed", concept.Keyword);
        await _bot.SendMessage(chatId, $"🗑 已忽略「{concept.Keyword}」", cancellationToken: ct);
    }

    // ── 推播待審核概念 ───────────────────────────────────────────────

    private async Task PushPendingConceptsAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StockBotDbContext>();

        var pending = await db.Set<DiscoveredConcept>()
            .Where(c => !c.IsApprovedAndPromoted)
            .OrderByDescending(c => c.AppearanceCount)
            .Take(5)
            .ToListAsync(ct);

        foreach (var concept in pending)
            await notifier.SendConceptReviewAsync(concept, ct);

        if (pending.Count > 0)
            logger.LogInformation("TelegramBotWorker: pushed {Count} concept review(s)", pending.Count);
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, HandleErrorSource source, CancellationToken ct)
    {
        logger.LogWarning(ex, "TelegramBotWorker: polling error (source={Source})", source);
        return Task.CompletedTask;
    }
}
