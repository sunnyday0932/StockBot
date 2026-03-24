using Microsoft.Extensions.Options;
using StockBot.Infrastructure.Alerting;
using StockBot.Infrastructure.Options;

namespace StockBot.Workers.Workers;

public sealed class SignalAnalyzerWorker(
    ISignalAnalyzer analyzer,
    IAlertNotifier notifier,
    IOptions<SignalAnalyzerOptions> options,
    ILogger<SignalAnalyzerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.AnalysisIntervalSeconds);
        logger.LogInformation("SignalAnalyzerWorker started (interval={Interval}s)", options.Value.AnalysisIntervalSeconds);

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var signals = await analyzer.AnalyzeAsync(stoppingToken);
                logger.LogInformation("SignalAnalyzerWorker: {Count} signal(s) triggered", signals.Count);

                foreach (var signal in signals)
                    await notifier.SendAlertAsync(signal, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "SignalAnalyzerWorker: unhandled error");
            }
        }
    }
}
