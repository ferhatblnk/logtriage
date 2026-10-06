using LogTriage.Configuration;
using Microsoft.Extensions.Options;

namespace LogTriage.Sync;

/// <summary>Runs the sync at start-up and then on every interval. A failed run is logged and retried on the next tick.</summary>
public sealed partial class SyncWorker(SyncService sync, IOptions<SyncOptions> options, ILogger<SyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IntervalSeconds));
        do
        {
            try
            {
                await sync.RunAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogFailed(exception.GetType().Name, exception.Message);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sync run failed and will be retried on the next interval: {Error}: {Detail}")]
    private partial void LogFailed(string error, string detail);
}

