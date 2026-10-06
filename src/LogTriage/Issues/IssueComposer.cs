using System.Globalization;
using System.Text.RegularExpressions;
using LogTriage.Configuration;
using LogTriage.Grouping;
using LogTriage.Redaction;
using Microsoft.Extensions.Options;

namespace LogTriage.Issues;

/// <summary>Writes the title and description of the issue for an error group.</summary>
public sealed partial class IssueComposer(IOptions<IssueOptions> issue, IOptions<LokiOptions> loki, RequestRedactor redactor)
{
    private const int TitleLimit = 255;
    private const int MessageLimit = 2000;
    private const string Ellipsis = "…";
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss 'UTC'";

    private static readonly char[] DanglingSeparators = [' ', ':', '-', '|', ','];

    public IssueDraft Compose(ErrorGroup group, TimeSpan window)
    {
        var settings = issue.Value;
        var latest = group.Latest;
        var blocks = new List<IssueBlock>
        {
            new ParagraphBlock("Opened automatically by LogTriage from an error group found in Loki."),
            new FactsBlock(Facts(group, window)),
        };

        if (latest.Message.Length > 0)
        {
            blocks.Add(new HeadingBlock("Message"));
            blocks.Add(new ParagraphBlock(Truncate(latest.Message, MessageLimit)));
        }

        if (latest.StackTrace.Length > 0)
        {
            blocks.Add(new HeadingBlock("Stack trace"));
            blocks.Add(new CodeBlock(Truncate(latest.StackTrace, settings.MaxStackTraceLength)));
        }

        if (settings.IncludeRequestData)
            AddRequest(blocks, latest, settings.MaxRequestBodyLength);

        blocks.Add(new HeadingBlock("Find it in Loki"));
        blocks.Add(new CodeBlock(LokiQuery(group)));

        return new IssueDraft(group.Fingerprint, Title(group, settings.TitleTemplate), blocks);
    }

    private static List<Fact> Facts(ErrorGroup group, TimeSpan window)
    {
        var facts = new List<Fact> { new("Service", group.Service) };
        if (group.Environment.Length > 0)
            facts.Add(new Fact("Environment", group.Environment));

        facts.Add(new Fact("Exception", group.ExceptionType));

        var request = $"{group.Method} {group.Route}".Trim();
        if (request.Length > 0)
            facts.Add(new Fact("Request", request));

        if (group.StatusCode is { } status)
            facts.Add(new Fact("Status", status.ToString(CultureInfo.InvariantCulture)));

        facts.Add(new Fact("Occurrences", string.Create(CultureInfo.InvariantCulture, $"{group.Count} in the last {Describe(window)}")));
        facts.Add(new Fact("First seen", Format(group.FirstSeen)));
        facts.Add(new Fact("Last seen", Format(group.LastSeen)));
        facts.Add(new Fact("Fingerprint", group.Fingerprint));
        return facts;
    }

    private void AddRequest(List<IssueBlock> blocks, ErrorEvent latest, int bodyLimit)
    {
        var request = redactor.Redact(latest.Query, latest.Body);
        if (request.Withheld)
        {
            blocks.Add(new HeadingBlock("Latest request"));
            blocks.Add(new ParagraphBlock("Request data was withheld because it looks like it contains payment details."));
            return;
        }

        var line = $"{latest.Method} {latest.Path}".Trim();
        if (request.Query.Length > 0)
            line += $"?{request.Query}";

        var text = string.Join("\n\n", new[] { line, Truncate(request.Body, bodyLimit) }.Where(static part => part.Length > 0));
        if (text.Length == 0)
            return;

        blocks.Add(new HeadingBlock("Latest request"));
        blocks.Add(new CodeBlock(text));
    }

    private string LokiQuery(ErrorGroup group)
    {
        var service = group.Service.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        var type = group.ExceptionType.Replace("`", "", StringComparison.Ordinal);
        return $"{{{loki.Value.ServiceLabel}=\"{service}\"}} |= `{type}`";
    }

    private static string Title(ErrorGroup group, string template)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["service"] = group.Service,
            ["environment"] = group.Environment,
            ["exceptionType"] = ShortName(group.ExceptionType),
            ["exceptionFullType"] = group.ExceptionType,
            ["method"] = group.Method,
            ["route"] = group.Route,
            ["status"] = group.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "",
            ["message"] = group.Latest.Message.Split('\n', 2)[0].Trim(),
        };

        var rendered = Placeholders().Replace(template, match => values.GetValueOrDefault(match.Groups[1].Value, match.Value));
        var tidy = string.Join(' ', rendered.Split(' ', StringSplitOptions.RemoveEmptyEntries)).TrimEnd(DanglingSeparators);
        return Truncate(tidy, TitleLimit);
    }

    private static string ShortName(string type) => type[(type.LastIndexOfAny(['.', '+']) + 1)..];

    private static string Describe(TimeSpan window) => window.TotalMinutes < 120
        ? string.Create(CultureInfo.InvariantCulture, $"{window.TotalMinutes:0} minutes")
        : string.Create(CultureInfo.InvariantCulture, $"{window.TotalHours:0.#} hours");

    private static string Format(DateTimeOffset time) => time.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture);

    private static string Truncate(string text, int limit) =>
        text.Length <= limit ? text : string.Concat(text.AsSpan(0, Math.Max(0, limit - 1)), Ellipsis);

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholders();
}
