using LogTriage.Configuration;
using LogTriage.Grouping;
using Microsoft.Extensions.Options;

namespace LogTriage.Loki;

/// <summary>
/// Reads every error in a time window. Loki caps the lines returned per query, so the window is walked
/// backwards page by page: each page ends where the previous one stopped.
/// </summary>
public sealed class ErrorReader(LokiClient loki, ErrorEventParser parser, IOptions<LokiOptions> options)
{
    public async Task<ErrorReadResult> ReadAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var events = new List<ErrorEvent>();
        var startNs = LokiTime.ToNanoseconds(from);
        var endNs = LokiTime.ToNanoseconds(to);
        var boundaryNs = long.MinValue;
        var seenAtBoundary = new HashSet<string>(StringComparer.Ordinal);
        var scanned = 0;

        while (true)
        {
            var page = await loki.QueryRangeAsync(startNs, endNs, settings.PageSize, cancellationToken);
            var fresh = 0;

            foreach (var entry in page)
            {
                if (entry.TimestampNs == boundaryNs && seenAtBoundary.Contains(Identity(entry)))
                    continue;

                fresh++;
                if (parser.Parse(entry) is { } error)
                    events.Add(error);
            }

            scanned += fresh;
            if (page.Count < settings.PageSize)
                return new ErrorReadResult(events, false);

            if (fresh == 0 || scanned >= settings.MaxEvents)
                return new ErrorReadResult(events, true);

            var oldestNs = page[^1].TimestampNs;
            if (oldestNs != boundaryNs)
                seenAtBoundary.Clear();

            foreach (var entry in page)
            {
                if (entry.TimestampNs == oldestNs)
                    seenAtBoundary.Add(Identity(entry));
            }

            boundaryNs = oldestNs;
            endNs = oldestNs + 1;
        }
    }

    private static string Identity(LokiEntry entry) => $"{entry.Stream}\n{entry.Line}";
}
