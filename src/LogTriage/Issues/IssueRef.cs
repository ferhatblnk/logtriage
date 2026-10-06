namespace LogTriage.Issues;

/// <summary>An issue that exists in the tracker.</summary>
public sealed record IssueRef(string Key, string Url);
