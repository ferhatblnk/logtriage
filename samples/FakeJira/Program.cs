using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FakeJira;

// Demo only. Implements the two Jira Cloud REST calls LogTriage makes and shows what was created,
// so the pipeline can be tried end to end without a Jira account. It is not a Jira emulator.

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IssueStore>();

var app = builder.Build();

app.MapGet("/", static (IssueStore store) => Results.Content(Pages.Board(store.All()), "text/html"));

app.MapGet("/browse/{key}", static (string key, IssueStore store) => store.Find(key) is { } issue
    ? Results.Content(Pages.Issue(issue), "text/html")
    : Results.NotFound());

app.MapGet("/rest/api/3/search/jql", static (string? jql, HttpRequest request, IssueStore store) =>
{
    if (!IsAuthenticated(request))
        return Results.Unauthorized();

    var label = Regex.Match(jql ?? "", "labels\\s*=\\s*\"(?<label>[^\"]+)\"").Groups["label"].Value;
    var issues = store.All()
        .Where(issue => issue.Labels.Contains(label))
        .Select(static issue => new { id = issue.Id, key = issue.Key, fields = new { summary = issue.Summary } });

    return Results.Ok(new { issues, isLast = true });
});

app.MapPost("/rest/api/3/issue", static (JsonObject body, HttpRequest request, IssueStore store) =>
{
    if (!IsAuthenticated(request))
        return Results.Unauthorized();

    var fields = body["fields"] as JsonObject;
    var project = fields?["project"]?["key"]?.GetValue<string>();
    var summary = fields?["summary"]?.GetValue<string>();
    if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(summary))
        return Results.BadRequest(new { errors = new { summary = "project and summary are required" } });

    var issue = store.Add(
        project,
        summary,
        fields?["issuetype"]?["name"]?.GetValue<string>() ?? "Task",
        [.. (fields?["labels"] as JsonArray ?? []).Select(static label => label?.GetValue<string>() ?? "")],
        fields?["description"]?.DeepClone());

    return Results.Json(new { id = issue.Id, key = issue.Key, self = $"/rest/api/3/issue/{issue.Id}" }, statusCode: StatusCodes.Status201Created);
});

app.Run();

static bool IsAuthenticated(HttpRequest request) =>
    request.Headers.Authorization.ToString().StartsWith("Basic ", StringComparison.Ordinal);
