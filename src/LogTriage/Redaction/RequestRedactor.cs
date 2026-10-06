using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LogTriage.Configuration;
using Microsoft.Extensions.Options;

namespace LogTriage.Redaction;

/// <summary>
/// Cleans request data before it leaves for the issue tracker: values of sensitive fields are masked,
/// and a request that looks like it carries payment details is withheld entirely.
/// </summary>
public sealed class RequestRedactor
{
    private const string Mask = "***";

    private static readonly JsonSerializerOptions Readable = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string[] sensitiveKeys;
    private readonly Regex? looseFields;

    public RequestRedactor(IOptions<IssueOptions> options)
    {
        sensitiveKeys = options.Value.SensitiveKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sensitiveKeys.Length == 0)
            return;

        var words = string.Join('|', sensitiveKeys.Select(Regex.Escape));
        looseFields = new Regex(
            $$"""(?<key>[\w.\-]*(?:{{words}})[\w.\-]*)(?<separator>"?\s*[:=]\s*)(?<value>"(?:[^"\\]|\\.)*"?|[^&,;\r\n}]+)""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    }

    public RedactedRequest Redact(string query, string body)
    {
        if (PaymentData.AppearsIn(query) || PaymentData.AppearsIn(body))
            return RedactedRequest.Hidden;

        return new RedactedRequest(RedactQuery(query), RedactBody(body), false);
    }

    private string RedactQuery(string query) =>
        string.Join('&', query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(RedactPair));

    private string RedactPair(string pair)
    {
        var separator = pair.IndexOf('=');
        if (separator < 0)
            return pair;

        var key = pair[..separator];
        return IsSensitive(Uri.UnescapeDataString(key)) ? $"{key}={Mask}" : pair;
    }

    private string RedactBody(string body)
    {
        if (body.Length == 0)
            return body;

        if (ParseJson(body) is { } node and (JsonObject or JsonArray))
        {
            MaskValues(node);
            return node.ToJsonString(Readable);
        }

        return looseFields is null ? body : looseFields.Replace(body, "${key}${separator}" + Mask);
    }

    private static JsonNode? ParseJson(string body)
    {
        try
        {
            return JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void MaskValues(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject fields:
                foreach (var name in fields.Select(static field => field.Key).ToList())
                {
                    if (IsSensitive(name))
                        fields[name] = Mask;
                    else
                        MaskValues(fields[name]);
                }

                break;

            case JsonArray items:
                foreach (var item in items)
                    MaskValues(item);

                break;
        }
    }

    private bool IsSensitive(string key) =>
        sensitiveKeys.Any(word => key.Contains(word, StringComparison.OrdinalIgnoreCase));
}
