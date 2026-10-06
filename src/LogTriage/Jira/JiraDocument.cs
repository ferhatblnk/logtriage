using System.Text.Json.Nodes;
using LogTriage.Issues;

namespace LogTriage.Jira;

/// <summary>Renders issue blocks as an Atlassian Document Format (ADF) document, the format Jira Cloud's REST API v3 expects.</summary>
internal static class JiraDocument
{
    private const int HeadingLevel = 3;

    public static JsonObject From(IEnumerable<IssueBlock> blocks) => new()
    {
        ["type"] = "doc",
        ["version"] = 1,
        ["content"] = Array(blocks.Select(Render).OfType<JsonNode>()),
    };

    private static JsonObject? Render(IssueBlock block) => block switch
    {
        HeadingBlock { Text.Length: > 0 } heading => Node("heading", [Text(heading.Text)], new JsonObject { ["level"] = HeadingLevel }),
        ParagraphBlock { Text.Length: > 0 } paragraph => Node("paragraph", Lines(paragraph.Text)),
        CodeBlock { Text.Length: > 0 } code => Node("codeBlock", [Text(code.Text)]),
        FactsBlock { Facts.Count: > 0 } facts => Node("bulletList", facts.Facts.Select(Item)),
        _ => null,
    };

    private static JsonObject Item(Fact fact) =>
        Node("listItem", [Node("paragraph", [Text($"{fact.Label}: ", "strong"), Text(fact.Value.Length > 0 ? fact.Value : "-")])]);

    private static List<JsonNode> Lines(string text)
    {
        var nodes = new List<JsonNode>();
        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            if (nodes.Count > 0)
                nodes.Add(new JsonObject { ["type"] = "hardBreak" });

            if (line.Length > 0)
                nodes.Add(Text(line));
        }

        return nodes;
    }

    private static JsonObject Node(string type, IEnumerable<JsonNode> content, JsonObject? attributes = null)
    {
        var node = new JsonObject { ["type"] = type };
        if (attributes is not null)
            node["attrs"] = attributes;

        node["content"] = Array(content);
        return node;
    }

    private static JsonObject Text(string text, string? mark = null)
    {
        var node = new JsonObject { ["type"] = "text", ["text"] = text };
        if (mark is not null)
            node["marks"] = new JsonArray(new JsonObject { ["type"] = mark });

        return node;
    }

    private static JsonArray Array(IEnumerable<JsonNode> nodes) => new([.. nodes]);
}
