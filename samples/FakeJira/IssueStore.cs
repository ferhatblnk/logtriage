using System.Globalization;
using System.Text.Json.Nodes;

namespace FakeJira;

public sealed record StoredIssue(
    string Id,
    string Key,
    string Summary,
    string IssueType,
    IReadOnlyList<string> Labels,
    JsonNode? Description,
    DateTimeOffset Created);

/// <summary>Keeps the demo's issues in memory.</summary>
public sealed class IssueStore
{
    private const int FirstId = 10000;

    private readonly Lock gate = new();
    private readonly List<StoredIssue> issues = [];

    public StoredIssue Add(string project, string summary, string issueType, IReadOnlyList<string> labels, JsonNode? description)
    {
        lock (gate)
        {
            var number = issues.Count + 1;
            var issue = new StoredIssue(
                (FirstId + number).ToString(CultureInfo.InvariantCulture),
                string.Create(CultureInfo.InvariantCulture, $"{project}-{number}"),
                summary,
                issueType,
                labels,
                description,
                DateTimeOffset.UtcNow);

            issues.Add(issue);
            return issue;
        }
    }

    public IReadOnlyList<StoredIssue> All()
    {
        lock (gate)
            return [.. issues];
    }

    public StoredIssue? Find(string key) =>
        All().FirstOrDefault(issue => string.Equals(issue.Key, key, StringComparison.OrdinalIgnoreCase));
}
