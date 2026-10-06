namespace LogTriage.Grouping;

/// <summary>A single logged exception.</summary>
/// <param name="Route">The route template, or the path with identifiers replaced when no template was logged.</param>
/// <param name="Path">The raw request path, identifiers included.</param>
public sealed record ErrorEvent(
    DateTimeOffset Timestamp,
    string Service,
    string Environment,
    string ExceptionType,
    string Message,
    string StackTrace,
    string Method,
    string Route,
    string Path,
    string Query,
    string Body,
    int? StatusCode);
