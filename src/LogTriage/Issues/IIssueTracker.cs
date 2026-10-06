namespace LogTriage.Issues;

/// <summary>The issue tracker errors are reported to.</summary>
public interface IIssueTracker
{
    /// <summary>Finds the issue that was opened for an error group, whatever its current state.</summary>
    Task<IssueRef?> FindAsync(string fingerprint, CancellationToken cancellationToken);

    /// <summary>Creates the issue and tags it so <see cref="FindAsync"/> finds it later.</summary>
    Task<IssueRef> CreateAsync(IssueDraft draft, CancellationToken cancellationToken);
}
