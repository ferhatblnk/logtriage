using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace LogTriage.Issues;

/// <summary>
/// Remembers which error group already has an issue, so the tracker is asked only once per group.
/// It lives in memory: after a restart the answers come back from the tracker's fingerprint label.
/// </summary>
public sealed class IssueRegistry
{
    private readonly ConcurrentDictionary<string, IssueRef> issues = new(StringComparer.Ordinal);

    public bool TryGet(string fingerprint, [MaybeNullWhen(false)] out IssueRef issue) =>
        issues.TryGetValue(fingerprint, out issue);

    public void Set(string fingerprint, IssueRef issue) => issues[fingerprint] = issue;
}
