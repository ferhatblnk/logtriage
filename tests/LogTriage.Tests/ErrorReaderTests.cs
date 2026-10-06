using System.Globalization;
using System.Text.Json;
using System.Web;
using LogTriage.Configuration;
using LogTriage.Loki;
using LogTriage.Tests.Support;
using Microsoft.Extensions.Options;

namespace LogTriage.Tests;

public class ErrorReaderTests
{
    private const string Line = """{"exception.type":"System.TimeoutException","url.path":"/payments"}""";

    private static readonly DateTimeOffset To = Samples.Noon;
    private static readonly DateTimeOffset From = Samples.Noon.AddHours(-1);

    [Fact]
    public async Task Reads_a_window_that_fits_in_one_page()
    {
        var loki = new FakeLoki(Entries(3));

        var result = await Reader(loki, pageSize: 10).ReadAsync(From, To, CancellationToken.None);

        Assert.Equal(3, result.Events.Count);
        Assert.False(result.Truncated);
        var request = Assert.Single(loki.Handler.Requests);
        var query = HttpUtility.ParseQueryString(request.Uri.Query);
        Assert.Equal("/loki/api/v1/query_range", request.Uri.AbsolutePath);
        Assert.Equal("backward", query["direction"]);
        Assert.Equal("10", query["limit"]);
        Assert.Equal(LokiTime.ToNanoseconds(From).ToString(CultureInfo.InvariantCulture), query["start"]);
        Assert.Equal(LokiTime.ToNanoseconds(To).ToString(CultureInfo.InvariantCulture), query["end"]);
        Assert.Equal(new LokiOptions().Query, query["query"]);
    }

    [Fact]
    public async Task Walks_backwards_through_every_page()
    {
        var loki = new FakeLoki(Entries(25));

        var result = await Reader(loki, pageSize: 10).ReadAsync(From, To, CancellationToken.None);

        Assert.Equal(25, result.Events.Count);
        Assert.Equal(25, result.Events.Select(error => error.Timestamp).Distinct().Count());
        Assert.False(result.Truncated);
    }

    [Fact]
    public async Task Does_not_lose_or_repeat_lines_that_share_a_timestamp_across_a_page_border()
    {
        var shared = LokiTime.ToNanoseconds(To.AddMinutes(-5));
        var entries = Entries(4)
            .Concat(Enumerable.Range(0, 5).Select(index => new FakeEntry(shared, Line.Replace("/payments", $"/payments/{index}"))))
            .Concat(Entries(4, startMinutes: 10))
            .ToList();
        var loki = new FakeLoki(entries);

        var result = await Reader(loki, pageSize: 6).ReadAsync(From, To, CancellationToken.None);

        Assert.Equal(13, result.Events.Count);
        Assert.Equal(13, result.Events.Select(error => (error.Timestamp, error.Path)).Distinct().Count());
    }

    [Fact]
    public async Task Stops_at_the_event_limit_and_reports_truncation()
    {
        var loki = new FakeLoki(Entries(50));

        var result = await Reader(loki, pageSize: 10, maxEvents: 20).ReadAsync(From, To, CancellationToken.None);

        Assert.True(result.Truncated);
        Assert.InRange(result.Events.Count, 20, 49);
        Assert.Equal(result.Events.Count, result.Events.Select(error => error.Timestamp).Distinct().Count());
        Assert.Equal(3, loki.Handler.Requests.Count);
    }

    [Fact]
    public async Task Skips_lines_that_are_not_errors()
    {
        var entries = Entries(2).Append(new FakeEntry(LokiTime.ToNanoseconds(To.AddMinutes(-20)), """{"level":"info"}""")).ToList();

        var result = await Reader(new FakeLoki(entries), pageSize: 10).ReadAsync(From, To, CancellationToken.None);

        Assert.Equal(2, result.Events.Count);
    }

    [Fact]
    public async Task Rejects_metric_queries_and_error_responses()
    {
        var metric = new StubHandler(_ => StubHandler.Json("""{"status":"success","data":{"resultType":"matrix","result":[]}}"""));
        var failing = new StubHandler(_ => StubHandler.Json("parse error", System.Net.HttpStatusCode.BadRequest));

        var metricError = await Assert.ThrowsAsync<LokiException>(() => Reader(metric, 10, 100).ReadAsync(From, To, CancellationToken.None));
        var failingError = await Assert.ThrowsAsync<LokiException>(() => Reader(failing, 10, 100).ReadAsync(From, To, CancellationToken.None));

        Assert.Contains("log query", metricError.Message);
        Assert.Equal("Loki answered 400: parse error", failingError.Message);
    }

    private static List<FakeEntry> Entries(int count, int startMinutes = 1) =>
        [.. Enumerable.Range(0, count).Select(index => new FakeEntry(LokiTime.ToNanoseconds(To.AddMinutes(-startMinutes).AddSeconds(-index)), Line))];

    private static ErrorReader Reader(FakeLoki loki, int pageSize, int maxEvents = 1000) => Reader(loki.Handler, pageSize, maxEvents);

    private static ErrorReader Reader(StubHandler handler, int pageSize, int maxEvents)
    {
        var options = Options.Create(new LokiOptions { PageSize = pageSize, MaxEvents = maxEvents });
        var client = new LokiClient(new StubClientFactory(handler, "http://loki.test/"), options);
        return new ErrorReader(client, new ErrorEventParser(options, Options.Create(new FieldOptions())), options);
    }

    private sealed record FakeEntry(long TimestampNs, string Line);

    /// <summary>Answers query_range the way Loki does: the newest lines in [start, end), capped at the limit.</summary>
    private sealed class FakeLoki
    {
        public FakeLoki(IReadOnlyList<FakeEntry> entries) => Handler = new StubHandler(request =>
        {
            var query = HttpUtility.ParseQueryString(request.Uri.Query);
            var start = long.Parse(query["start"]!, CultureInfo.InvariantCulture);
            var end = long.Parse(query["end"]!, CultureInfo.InvariantCulture);
            var limit = int.Parse(query["limit"]!, CultureInfo.InvariantCulture);

            var values = entries
                .Where(entry => entry.TimestampNs >= start && entry.TimestampNs < end)
                .OrderByDescending(entry => entry.TimestampNs)
                .Take(limit)
                .Select(entry => new[] { entry.TimestampNs.ToString(CultureInfo.InvariantCulture), entry.Line });

            var stream = new { stream = new { service_name = "payments-api", env = "test" }, values };
            return StubHandler.Json(JsonSerializer.Serialize(new { status = "success", data = new { resultType = "streams", result = new[] { stream } } }));
        });

        public StubHandler Handler { get; }
    }
}
