namespace LogTriage.Grouping;

/// <summary>All occurrences of the same error inside a time window.</summary>
/// <param name="Latest">The most recent occurrence; its message, stack trace and request describe the group.</param>
public sealed record ErrorGroup(
    string Fingerprint,
    string Service,
    string Environment,
    string ExceptionType,
    string Method,
    string Route,
    int? StatusCode,
    int Count,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    ErrorEvent Latest);
