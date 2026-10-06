using System.Globalization;
using System.Text.Json;
using LogTriage.Configuration;
using Microsoft.Extensions.Options;

namespace LogTriage.Loki;

/// <summary>Runs the configured log query against Loki's <c>query_range</c> endpoint.</summary>
public sealed class LokiClient(IHttpClientFactory clients, IOptions<LokiOptions> options)
{
    public const string ClientName = "loki";

    private const int ErrorBodyLimit = 500;

    /// <summary>Returns at most <paramref name="limit"/> lines in the range, newest first.</summary>
    public async Task<IReadOnlyList<LokiEntry>> QueryRangeAsync(long startNs, long endNs, int limit, CancellationToken cancellationToken)
    {
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"loki/api/v1/query_range?query={Uri.EscapeDataString(options.Value.Query)}&start={startNs}&end={endNs}&limit={limit}&direction=backward");

        using var response = await clients.CreateClient(ClientName).GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new LokiException($"Loki answered {(int)response.StatusCode}: {Shorten(error)}");
        }

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        return Read(document.RootElement);
    }

    private static List<LokiEntry> Read(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("result", out var result))
            throw new LokiException("Loki returned a response without data.");

        if (!data.TryGetProperty("resultType", out var type) || type.GetString() != "streams")
            throw new LokiException("Loki:Query must be a log query, not a metric query.");

        var entries = new List<LokiEntry>();
        foreach (var stream in result.EnumerateArray())
        {
            if (!stream.TryGetProperty("stream", out var labelSet) || !stream.TryGetProperty("values", out var values))
                continue;

            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var label in labelSet.EnumerateObject())
                labels[label.Name] = label.Value.GetString() ?? "";

            var streamKey = labelSet.GetRawText();
            foreach (var value in values.EnumerateArray())
            {
                var timestamp = long.Parse(value[0].GetString() ?? "0", CultureInfo.InvariantCulture);
                entries.Add(new LokiEntry(timestamp, value[1].GetString() ?? "", streamKey, labels));
            }
        }

        entries.Sort(static (left, right) => right.TimestampNs.CompareTo(left.TimestampNs));
        return entries;
    }

    private static string Shorten(string text) =>
        text.Length <= ErrorBodyLimit ? text : text[..ErrorBodyLimit];
}
