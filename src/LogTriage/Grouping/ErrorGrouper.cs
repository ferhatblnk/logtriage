namespace LogTriage.Grouping;

public static class ErrorGrouper
{
    /// <summary>Groups events by fingerprint, most frequent first.</summary>
    public static IReadOnlyList<ErrorGroup> Group(IEnumerable<ErrorEvent> events) =>
    [
        .. events
            .GroupBy(Fingerprint.Of)
            .Select(ToGroup)
            .OrderByDescending(static group => group.Count)
            .ThenByDescending(static group => group.LastSeen)
            .ThenBy(static group => group.Fingerprint, StringComparer.Ordinal),
    ];

    private static ErrorGroup ToGroup(IGrouping<string, ErrorEvent> occurrences)
    {
        var latest = occurrences.MaxBy(static error => error.Timestamp)!;
        return new ErrorGroup(
            occurrences.Key,
            latest.Service,
            latest.Environment,
            latest.ExceptionType,
            latest.Method,
            latest.Route,
            latest.StatusCode,
            occurrences.Count(),
            occurrences.Min(static error => error.Timestamp),
            latest.Timestamp,
            latest);
    }
}
