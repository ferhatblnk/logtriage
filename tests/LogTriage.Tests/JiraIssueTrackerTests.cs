using System.Net;
using System.Text.Json.Nodes;
using LogTriage.Configuration;
using LogTriage.Issues;
using LogTriage.Jira;
using LogTriage.Tests.Support;
using Microsoft.Extensions.Options;

namespace LogTriage.Tests;

public class JiraIssueTrackerTests
{
    private static readonly JiraOptions Settings = new()
    {
        BaseUrl = "https://example.atlassian.net",
        Email = "bot@example.com",
        ApiToken = "token",
        ProjectKey = "OPS",
        IssueType = "Bug",
        AssigneeAccountId = "account-1",
        ParentKey = "OPS-7",
        Labels = "logtriage, needs triage",
    };

    private static readonly IssueDraft Draft = new("0123456789abcdef", "[orders-api] TimeoutException: POST /payments",
    [
        new ParagraphBlock("first line\nsecond line"),
        new FactsBlock([new Fact("Service", "orders-api")]),
        new HeadingBlock("Stack trace"),
        new CodeBlock("at Payments.Pay()"),
    ]);

    [Fact]
    public async Task Find_searches_by_the_fingerprint_label()
    {
        var handler = new StubHandler(_ => StubHandler.Json("""{"issues":[{"id":"10001","key":"OPS-12"}]}"""));

        var issue = await Tracker(handler).FindAsync("0123456789abcdef", CancellationToken.None);

        Assert.Equal(new IssueRef("OPS-12", "https://example.atlassian.net/browse/OPS-12"), issue);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/rest/api/3/search/jql", request.Uri.AbsolutePath);
        Assert.Contains("labels = \"logtriage-0123456789abcdef\"", Uri.UnescapeDataString(request.Uri.Query));
    }

    [Fact]
    public async Task Find_returns_null_when_nothing_matches()
    {
        var handler = new StubHandler(_ => StubHandler.Json("""{"issues":[]}"""));

        Assert.Null(await Tracker(handler).FindAsync("0123456789abcdef", CancellationToken.None));
    }

    [Fact]
    public async Task Create_posts_the_fields_and_an_adf_description()
    {
        var handler = new StubHandler(_ => StubHandler.Json("""{"id":"10002","key":"OPS-13"}""", HttpStatusCode.Created));

        var issue = await Tracker(handler).CreateAsync(Draft, CancellationToken.None);

        Assert.Equal("OPS-13", issue.Key);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/rest/api/3/issue", request.Uri.AbsolutePath);

        var fields = JsonNode.Parse(request.Body)!["fields"]!;
        Assert.Equal("OPS", fields["project"]!["key"]!.GetValue<string>());
        Assert.Equal("Bug", fields["issuetype"]!["name"]!.GetValue<string>());
        Assert.Equal(Draft.Title, fields["summary"]!.GetValue<string>());
        Assert.Equal("account-1", fields["assignee"]!["accountId"]!.GetValue<string>());
        Assert.Equal("OPS-7", fields["parent"]!["key"]!.GetValue<string>());
        Assert.Equal(new[] { "logtriage", "needs-triage", "logtriage-0123456789abcdef" }, fields["labels"]!.AsArray().Select(label => label!.GetValue<string>()));

        var description = fields["description"]!;
        Assert.Equal("doc", description["type"]!.GetValue<string>());
        Assert.Equal(1, description["version"]!.GetValue<int>());
        Assert.Equal(new[] { "paragraph", "bulletList", "heading", "codeBlock" }, description["content"]!.AsArray().Select(node => node!["type"]!.GetValue<string>()));
        Assert.Equal(new[] { "text", "hardBreak", "text" }, description["content"]![0]!["content"]!.AsArray().Select(node => node!["type"]!.GetValue<string>()));
        Assert.Equal("strong", description["content"]![1]!["content"]![0]!["content"]![0]!["content"]![0]!["marks"]![0]!["type"]!.GetValue<string>());
        Assert.Equal(3, description["content"]![2]!["attrs"]!["level"]!.GetValue<int>());
    }

    [Fact]
    public async Task Create_omits_optional_fields_that_are_not_configured()
    {
        var handler = new StubHandler(_ => StubHandler.Json("""{"key":"OPS-14"}""", HttpStatusCode.Created));
        var settings = new JiraOptions { BaseUrl = Settings.BaseUrl, ProjectKey = "OPS", BrowseUrl = "https://jira.example.com/" };

        var issue = await Tracker(handler, settings).CreateAsync(Draft, CancellationToken.None);

        var fields = JsonNode.Parse(handler.Requests[0].Body)!["fields"]!.AsObject();
        Assert.False(fields.ContainsKey("assignee"));
        Assert.False(fields.ContainsKey("parent"));
        Assert.Equal("https://jira.example.com/browse/OPS-14", issue.Url);
    }

    [Fact]
    public async Task Failures_surface_the_status_and_a_shortened_body()
    {
        var handler = new StubHandler(_ => StubHandler.Json("{\"errors\":{\"assignee\":\"" + new string('x', 2000) + "\"}}", HttpStatusCode.BadRequest));

        var error = await Assert.ThrowsAsync<IssueTrackerException>(() => Tracker(handler).CreateAsync(Draft, CancellationToken.None));

        Assert.StartsWith("Jira answered 400:", error.Message);
        Assert.True(error.Message.Length < 600);
    }

    private static JiraIssueTracker Tracker(StubHandler handler, JiraOptions? settings = null) =>
        new(new StubClientFactory(handler, "https://example.atlassian.net/"), Options.Create(settings ?? Settings));
}
