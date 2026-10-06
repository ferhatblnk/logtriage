namespace LogTriage.Api;

public sealed record GroupsResponse(int WindowMinutes, bool DryRun, bool Truncated, IReadOnlyList<GroupDto> Groups);

/// <param name="Eligible">True when the group passes the sync policy and gets (or already has) an issue.</param>
public sealed record GroupDto(
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
    string Message,
    bool Eligible,
    string? IssueKey,
    string? IssueUrl);
