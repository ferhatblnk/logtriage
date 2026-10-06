using System.Globalization;
using System.Text.Json;
using LogTriage.Configuration;
using LogTriage.Issues;
using LogTriage.Loki;
using LogTriage.Redaction;
using LogTriage.Sync;
using LogTriage.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LogTriage.Tests;

public class SyncServiceTests
{
    [Fact]
    public async Task Opens_one_issue_per_eligible_group_and_never_twice()
    {
        var tracker = new FakeTracker();
        var sync = Sync(tracker, new SyncOptions { DryRun = false }, Line("/orders/1", 500), Line("/orders/2", 500), Line("/payments", 503), Line("/orders/1/invoice", 404));

        var first = await sync.RunAsync(CancellationToken.None);
        var second = await sync.RunAsync(CancellationToken.None);

        Assert.Equal(new SyncReport(4, 3, 2, 2, 0, false, false), first);
        Assert.Equal(new SyncReport(4, 3, 2, 0, 0, false, false), second);
        Assert.Equal(2, tracker.Created.Count);
        Assert.Equal(2, tracker.Lookups);
        Assert.Contains(tracker.Created, draft => draft.Title == "[orders-api] TimeoutException: GET /orders/{id}");
    }

    [Fact]
    public async Task Links_to_an_issue_that_already_exists_in_the_tracker()
    {
        var tracker = new FakeTracker { FindExisting = true };
        var sync = Sync(tracker, new SyncOptions { DryRun = false }, Line("/payments", 500));

        var first = await sync.RunAsync(CancellationToken.None);
        var second = await sync.RunAsync(CancellationToken.None);

        Assert.Equal(1, first.Linked);
        Assert.Equal(0, first.Opened);
        Assert.Empty(tracker.Created);
        Assert.Equal(1, tracker.Lookups);
        Assert.Equal(0, second.Linked);
    }

    [Fact]
    public async Task Dry_run_never_touches_the_tracker_and_announces_each_group_once()
    {
        var tracker = new FakeTracker();
        var sync = Sync(tracker, new SyncOptions(), Line("/payments", 500), Line("/refunds", 500));

        var first = await sync.RunAsync(CancellationToken.None);
        var second = await sync.RunAsync(CancellationToken.None);

        Assert.True(first.DryRun);
        Assert.Equal(2, first.Opened);
        Assert.Equal(0, second.Opened);
        Assert.Equal(0, tracker.Lookups);
        Assert.Empty(tracker.Created);
    }

    [Fact]
    public async Task Opens_at_most_the_configured_number_of_issues_per_run_most_frequent_first()
    {
        var tracker = new FakeTracker();
        var lines = new[] { Line("/a", 500), Line("/b", 500), Line("/b", 500), Line("/c", 500), Line("/c", 500), Line("/c", 500) };
        var sync = Sync(tracker, new SyncOptions { DryRun = false, MaxIssuesPerRun = 2 }, lines);

        var first = await sync.RunAsync(CancellationToken.None);
        var second = await sync.RunAsync(CancellationToken.None);

        Assert.Equal(2, first.Opened);
        Assert.Equal(1, second.Opened);
        Assert.Equal(new[] { "GET /c", "GET /b", "GET /a" }, tracker.Created.Select(draft => draft.Title.Split(": ")[1]));
    }

    [Theory]
    [InlineData(500, 1, null, true)]
    [InlineData(499, 1, null, false)]
    [InlineData(null, 1, null, true)]
    [InlineData(500, 4, null, false)]
    [InlineData(500, 3, null, true)]
    [InlineData(500, 1, 1, false)]
    [InlineData(500, 1, -1, true)]
    public void Policy_checks_status_occurrences_and_start_time(int? status, int minOccurrences, int? notBeforeOffsetMinutes, bool expected)
    {
        var options = new SyncOptions
        {
            MinOccurrences = minOccurrences,
            NotBefore = notBeforeOffsetMinutes is { } offset ? Samples.Noon.AddMinutes(offset) : null,
        };

        Assert.Equal(expected, IssuePolicy.IsEligible(Samples.Group(count: 3, status: status), options));
    }

    private static string Line(string path, int status) => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["exception.type"] = "System.TimeoutException",
        ["http.request.method"] = "GET",
        ["url.path"] = path,
        ["http.response.status_code"] = status,
    });

    private static SyncService Sync(FakeTracker tracker, SyncOptions settings, params string[] lines)
    {
        var loki = Options.Create(new LokiOptions());
        var issue = Options.Create(new IssueOptions());
        var values = lines.Select((line, index) => new[] { (LokiTime.ToNanoseconds(Samples.Noon.AddMinutes(-1)) - index).ToString(CultureInfo.InvariantCulture), line });
        var response = JsonSerializer.Serialize(new
        {
            status = "success",
            data = new { resultType = "streams", result = new[] { new { stream = new { service_name = "orders-api" }, values } } },
        });

        var client = new LokiClient(new StubClientFactory(new StubHandler(_ => StubHandler.Json(response)), "http://loki.test/"), loki);
        var reader = new ErrorReader(client, new ErrorEventParser(loki, Options.Create(new FieldOptions())), loki);
        var composer = new IssueComposer(issue, loki, new RequestRedactor(issue));
        return new SyncService(reader, tracker, composer, new IssueRegistry(), Options.Create(settings), new FixedTime(Samples.Noon), NullLogger<SyncService>.Instance);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeTracker : IIssueTracker
    {
        public bool FindExisting { get; init; }

        public int Lookups { get; private set; }

        public List<IssueDraft> Created { get; } = [];

        public Task<IssueRef?> FindAsync(string fingerprint, CancellationToken cancellationToken)
        {
            Lookups++;
            return Task.FromResult(FindExisting ? new IssueRef("OPS-1", "https://example.test/browse/OPS-1") : null);
        }

        public Task<IssueRef> CreateAsync(IssueDraft draft, CancellationToken cancellationToken)
        {
            Created.Add(draft);
            return Task.FromResult(new IssueRef($"OPS-{Created.Count}", $"https://example.test/browse/OPS-{Created.Count}"));
        }
    }
}
