using LogTriage.Configuration;
using LogTriage.Grouping;

namespace LogTriage.Issues;

public static class IssuePolicy
{
    /// <summary>Decides whether an error group is worth an issue.</summary>
    public static bool IsEligible(ErrorGroup group, SyncOptions options) =>
        group.Count >= options.MinOccurrences
        && (group.StatusCode is null || group.StatusCode >= options.MinStatusCode)
        && (options.NotBefore is null || group.LastSeen >= options.NotBefore);
}
