using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LogTriage.Configuration;
using LogTriage.Issues;
using Microsoft.Extensions.Options;

namespace LogTriage.Jira;

/// <summary>
/// Jira Cloud over REST API v3. Every issue carries a label made of the group fingerprint;
/// searching for that label is how an existing issue is found, so no local database is needed.
/// </summary>
public sealed class JiraIssueTracker(IHttpClientFactory clients, IOptions<JiraOptions> options) : IIssueTracker
{
    public const string ClientName = "jira";

    private const int ErrorBodyLimit = 500;

    public async Task<IssueRef?> FindAsync(string fingerprint, CancellationToken cancellationToken)
    {
        var jql = $"labels = \"{FingerprintLabel(fingerprint)}\" ORDER BY created ASC";
        var url = $"rest/api/3/search/jql?jql={Uri.EscapeDataString(jql)}&fields=summary&maxResults=1";

        using var response = await clients.CreateClient(ClientName).GetAsync(url, cancellationToken);
        var body = await ReadAsync(response, cancellationToken);

        return body?["issues"] is JsonArray { Count: > 0 } issues && Key(issues[0]) is { } key ? Reference(key) : null;
    }

    public async Task<IssueRef> CreateAsync(IssueDraft draft, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var fields = new JsonObject
        {
            ["project"] = new JsonObject { ["key"] = settings.ProjectKey },
            ["issuetype"] = new JsonObject { ["name"] = settings.IssueType },
            ["summary"] = draft.Title,
            ["description"] = JiraDocument.From(draft.Body),
            ["labels"] = new JsonArray([.. Labels(draft.Fingerprint).Select(static label => JsonValue.Create(label))]),
        };

        if (settings.AssigneeAccountId.Length > 0)
            fields["assignee"] = new JsonObject { ["accountId"] = settings.AssigneeAccountId };

        if (settings.ParentKey.Length > 0)
            fields["parent"] = new JsonObject { ["key"] = settings.ParentKey };

        using var response = await clients.CreateClient(ClientName)
            .PostAsJsonAsync("rest/api/3/issue", new JsonObject { ["fields"] = fields }, cancellationToken);
        var body = await ReadAsync(response, cancellationToken);

        return Key(body) is { } key
            ? Reference(key)
            : throw new IssueTrackerException("Jira accepted the issue but returned no key.");
    }

    private IEnumerable<string> Labels(string fingerprint) => options.Value.Labels
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(static label => label.Replace(' ', '-'))
        .Append(FingerprintLabel(fingerprint))
        .Distinct(StringComparer.Ordinal);

    private string FingerprintLabel(string fingerprint) => options.Value.FingerprintLabelPrefix + fingerprint;

    private IssueRef Reference(string key)
    {
        var settings = options.Value;
        var site = (settings.BrowseUrl.Length > 0 ? settings.BrowseUrl : settings.BaseUrl).TrimEnd('/');
        return new IssueRef(key, $"{site}/browse/{key}");
    }

    private static string? Key(JsonNode? issue) =>
        issue is JsonObject fields && fields["key"] is JsonValue key && key.TryGetValue<string>(out var text) ? text : null;

    private static async Task<JsonNode?> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = text.Length <= ErrorBodyLimit ? text : text[..ErrorBodyLimit];
            throw new IssueTrackerException($"Jira answered {(int)response.StatusCode}: {detail}");
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new IssueTrackerException("Jira returned a response that is not JSON.");
        }
    }
}
