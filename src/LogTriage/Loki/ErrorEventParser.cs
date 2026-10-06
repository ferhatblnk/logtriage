using System.Globalization;
using System.Text.Json;
using LogTriage.Configuration;
using LogTriage.Grouping;
using Microsoft.Extensions.Options;

namespace LogTriage.Loki;

/// <summary>Turns a log line into an <see cref="ErrorEvent"/>. Lines without an exception type are not errors and are skipped.</summary>
public sealed class ErrorEventParser(IOptions<LokiOptions> loki, IOptions<FieldOptions> fields)
{
    private const string UnknownService = "unknown";

    public ErrorEvent? Parse(LokiEntry entry)
    {
        using var document = ParseJson(entry.Line);
        var body = document?.RootElement;
        var names = fields.Value;

        var exceptionType = Read(body, entry.Labels, names.ExceptionType);
        if (string.IsNullOrWhiteSpace(exceptionType))
            return null;

        var path = Read(body, entry.Labels, names.Path);
        var route = Read(body, entry.Labels, names.Route);
        if (route.Length == 0 && path.Length > 0)
            route = RouteNormalizer.Normalize(path);

        var status = Read(body, entry.Labels, names.StatusCode);

        return new ErrorEvent(
            LokiTime.FromNanoseconds(entry.TimestampNs),
            entry.Labels.TryGetValue(loki.Value.ServiceLabel, out var service) && service.Length > 0 ? service : UnknownService,
            entry.Labels.GetValueOrDefault(loki.Value.EnvironmentLabel, ""),
            exceptionType.Trim(),
            Read(body, entry.Labels, names.Message),
            Read(body, entry.Labels, names.StackTrace),
            Read(body, entry.Labels, names.Method).ToUpperInvariant(),
            route,
            path,
            Read(body, entry.Labels, names.Query),
            Read(body, entry.Labels, names.Body),
            int.TryParse(status, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) ? code : null);
    }

    private string Read(JsonElement? body, IReadOnlyDictionary<string, string> labels, string name)
    {
        if (name.Length == 0)
            return "";

        if (body is { } root)
        {
            var container = fields.Value.Root;
            if (container.Length > 0 && JsonFieldReader.Read(root, $"{container}.{name}") is { } nested)
                return nested;

            if (JsonFieldReader.Read(root, name) is { } value)
                return value;
        }

        return labels.GetValueOrDefault(LabelName(name), "");
    }

    private static string LabelName(string name) =>
        string.Concat(name.Select(static character => char.IsAsciiLetterOrDigit(character) ? character : '_'));

    private static JsonDocument? ParseJson(string line)
    {
        if (!line.AsSpan().TrimStart().StartsWith("{"))
            return null;

        try
        {
            return JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
