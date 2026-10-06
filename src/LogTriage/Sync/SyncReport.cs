namespace LogTriage.Sync;

/// <summary>What one sync run did.</summary>
/// <param name="Opened">Issues created, or in a dry run the issues that would have been created.</param>
/// <param name="Linked">Groups whose issue already existed in the tracker.</param>
public sealed record SyncReport(int Events, int Groups, int Eligible, int Opened, int Linked, bool Truncated, bool DryRun);
