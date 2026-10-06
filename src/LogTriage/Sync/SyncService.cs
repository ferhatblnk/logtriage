using LogTriage.Configuration;
using LogTriage.Grouping;
using LogTriage.Issues;
using LogTriage.Loki;
using Microsoft.Extensions.Options;

namespace LogTriage.Sync;

/// <summary>
/// One pass of the pipeline: read recent errors from Loki, group them, and make sure every
/// eligible group has exactly one issue.
/// </summary>
public sealed partial class SyncService(
    ErrorReader reader,
    IIssueTracker tracker,
    IssueComposer composer,
    IssueRegistry registry,
    IOptions<SyncOptions> options,
    TimeProvider time,
    ILogger<SyncService> logger)
{
    private readonly HashSet<string> announced = new(StringComparer.Ordinal);

    public async Task<SyncReport> RunAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var window = TimeSpan.FromMinutes(settings.LookbackMinutes);
        var now = time.GetUtcNow();

        var read = await reader.ReadAsync(now - window, now, cancellationToken);
        var groups = ErrorGrouper.Group(read.Events);
        var eligible = groups.Where(group => IssuePolicy.IsEligible(group, settings)).ToList();

        var opened = 0;
        var linked = 0;
        foreach (var group in eligible)
        {
            if (opened >= settings.MaxIssuesPerRun)
                break;

            if (registry.TryGet(group.Fingerprint, out _))
                continue;

            var draft = composer.Compose(group, window);
            if (settings.DryRun)
            {
                if (announced.Add(group.Fingerprint))
                {
                    LogWouldOpen(draft.Title, group.Fingerprint, group.Count);
                    opened++;
                }

                continue;
            }

            if (await tracker.FindAsync(group.Fingerprint, cancellationToken) is { } existing)
            {
                registry.Set(group.Fingerprint, existing);
                linked++;
                continue;
            }

            var created = await tracker.CreateAsync(draft, cancellationToken);
            registry.Set(group.Fingerprint, created);
            LogOpened(created.Key, draft.Title, group.Fingerprint, group.Count);
            opened++;
        }

        var report = new SyncReport(read.Events.Count, groups.Count, eligible.Count, opened, linked, read.Truncated, settings.DryRun);
        LogFinished(report.Events, report.Groups, report.Eligible, report.Opened, report.Linked, report.Truncated, report.DryRun);
        return report;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Dry run: would open \"{Title}\" (fingerprint {Fingerprint}, {Count} occurrences)")]
    private partial void LogWouldOpen(string title, string fingerprint, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Opened {Key} \"{Title}\" (fingerprint {Fingerprint}, {Count} occurrences)")]
    private partial void LogOpened(string key, string title, string fingerprint, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync finished: {Events} events, {Groups} groups, {Eligible} eligible, {Opened} opened, {Linked} linked, truncated={Truncated}, dryRun={DryRun}")]
    private partial void LogFinished(int events, int groups, int eligible, int opened, int linked, bool truncated, bool dryRun);
}
