using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace FakeJira;

/// <summary>The two HTML pages of the demo: the list of created issues and a single issue.</summary>
public static class Pages
{
    private const string Style = """
        :root { color-scheme: light dark; --ink: #172b4d; --muted: #5e6c84; --line: #dfe1e6; --surface: #ffffff; --page: #f4f5f7; --accent: #0c66e4; --code: #f1f2f4; }
        @media (prefers-color-scheme: dark) { :root { --ink: #dee4ea; --muted: #8c9bab; --line: #2c333a; --surface: #1d2125; --page: #161a1d; --accent: #579dff; --code: #22272b; } }
        * { box-sizing: border-box; }
        body { margin: 0; padding: 24px 16px; background: var(--page); color: var(--ink); font: 15px/1.5 system-ui, -apple-system, "Segoe UI", sans-serif; }
        main { max-width: 960px; margin: 0 auto; }
        h1 { font-size: 22px; margin: 0 0 4px; }
        h3 { font-size: 15px; margin: 20px 0 6px; }
        a { color: var(--accent); text-decoration: none; }
        a:hover { text-decoration: underline; }
        .note { color: var(--muted); margin: 0 0 20px; }
        .card { background: var(--surface); border: 1px solid var(--line); border-radius: 8px; padding: 16px 20px; overflow-x: auto; }
        table { width: 100%; border-collapse: collapse; }
        th, td { text-align: left; padding: 8px 10px; border-bottom: 1px solid var(--line); vertical-align: top; }
        th { color: var(--muted); font-weight: 600; font-size: 13px; }
        tr:last-child td { border-bottom: 0; }
        .label { display: inline-block; margin: 0 4px 4px 0; padding: 1px 8px; border-radius: 4px; background: var(--code); font-size: 12px; }
        pre { background: var(--code); padding: 12px; border-radius: 6px; overflow-x: auto; font: 13px/1.45 ui-monospace, SFMono-Regular, Menlo, monospace; white-space: pre-wrap; word-break: break-word; }
        ul { padding-left: 20px; }
        .empty { color: var(--muted); padding: 24px 0; text-align: center; }
        """;

    public static string Board(IReadOnlyList<StoredIssue> issues)
    {
        var rows = new StringBuilder();
        foreach (var issue in issues.Reverse())
        {
            rows.Append(CultureInfo.InvariantCulture, $"""
                <tr>
                  <td><a href="/browse/{Encode(issue.Key)}">{Encode(issue.Key)}</a></td>
                  <td>{Encode(issue.IssueType)}</td>
                  <td>{Encode(issue.Summary)}<br>{Labels(issue)}</td>
                  <td>{issue.Created:HH:mm:ss} UTC</td>
                </tr>
                """);
        }

        var content = issues.Count == 0
            ? """<p class="empty">No issues yet. They appear here as soon as LogTriage opens one.</p>"""
            : $"<table><thead><tr><th>Key</th><th>Type</th><th>Summary</th><th>Created</th></tr></thead><tbody>{rows}</tbody></table>";

        return Layout("Fake Jira", $"""
            <h1>Fake Jira</h1>
            <p class="note">Demo stand-in for Jira Cloud. Issues live in memory and the page refreshes every 5 seconds.</p>
            <div class="card">{content}</div>
            """, refresh: true);
    }

    public static string Issue(StoredIssue issue) => Layout(issue.Key, $"""
        <p class="note"><a href="/">&larr; All issues</a></p>
        <h1>{Encode(issue.Key)} &middot; {Encode(issue.Summary)}</h1>
        <p class="note">{Encode(issue.IssueType)} &middot; created {issue.Created:yyyy-MM-dd HH:mm:ss} UTC</p>
        <p>{Labels(issue)}</p>
        <div class="card">{Render(issue.Description)}</div>
        """, refresh: false);

    private static string Layout(string title, string body, bool refresh) => $"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          {(refresh ? """<meta http-equiv="refresh" content="5">""" : "")}
          <title>{Encode(title)}</title>
          <style>{Style}</style>
        </head>
        <body><main>{body}</main></body>
        </html>
        """;

    private static string Labels(StoredIssue issue) =>
        string.Concat(issue.Labels.Select(static label => $"""<span class="label">{Encode(label)}</span>"""));

    private static string Render(JsonNode? node)
    {
        if (node is not JsonObject element)
            return "";

        var children = string.Concat((element["content"] as JsonArray ?? []).Select(Render));
        return element["type"]?.GetValue<string>() switch
        {
            "doc" => children,
            "heading" => $"<h3>{children}</h3>",
            "paragraph" => $"<p>{children}</p>",
            "bulletList" => $"<ul>{children}</ul>",
            "listItem" => $"<li>{children}</li>",
            "codeBlock" => $"<pre>{children}</pre>",
            "hardBreak" => "<br>",
            "text" => Text(element),
            _ => children,
        };
    }

    private static string Text(JsonObject element)
    {
        var text = Encode(element["text"]?.GetValue<string>() ?? "");
        var strong = (element["marks"] as JsonArray ?? []).Any(static mark => mark?["type"]?.GetValue<string>() == "strong");
        return strong ? $"<strong>{text}</strong>" : text;
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
