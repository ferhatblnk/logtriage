namespace LogTriage.Issues;

/// <summary>An issue ready to be created in a tracker.</summary>
public sealed record IssueDraft(string Fingerprint, string Title, IReadOnlyList<IssueBlock> Body);
