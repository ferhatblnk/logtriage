namespace LogTriage.Configuration;

/// <summary>When the worker runs and which error groups deserve an issue.</summary>
public sealed class SyncOptions
{
    public const string Section = "Sync";

    /// <summary>On by default: groups are reported in the log and the API, nothing is written to the tracker.</summary>
    public bool DryRun { get; set; } = true;

    public int IntervalSeconds { get; set; } = 300;

    /// <summary>How far back each run reads. Occurrence counts in an issue refer to this window.</summary>
    public int LookbackMinutes { get; set; } = 60;

    /// <summary>Groups with a lower HTTP status are ignored. Groups without a status always qualify.</summary>
    public int MinStatusCode { get; set; } = 500;

    public int MinOccurrences { get; set; } = 1;

    /// <summary>Caps the issues opened in one run so a bad deploy cannot flood the tracker.</summary>
    public int MaxIssuesPerRun { get; set; } = 5;

    /// <summary>Groups last seen before this moment are ignored. Useful when switching the tool on.</summary>
    public DateTimeOffset? NotBefore { get; set; }
}
