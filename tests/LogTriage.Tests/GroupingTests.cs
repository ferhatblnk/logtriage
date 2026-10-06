using LogTriage.Grouping;
using LogTriage.Tests.Support;

namespace LogTriage.Tests;

public class GroupingTests
{
    [Theory]
    [InlineData("/orders/42", "/orders/{id}")]
    [InlineData("/orders/42/items/7?expand=all", "/orders/{id}/items/{id}")]
    [InlineData("/users/3f2504e0-4f89-11d3-9a0c-0305e82c3301/profile", "/users/{id}/profile")]
    [InlineData("/files/507f1f77bcf86cd799439011", "/files/{id}")]
    [InlineData("/orders/", "/orders")]
    [InlineData("/v2/orders", "/v2/orders")]
    [InlineData("/", "/")]
    public void Normalizer_replaces_identifiers(string path, string expected) =>
        Assert.Equal(expected, RouteNormalizer.Normalize(path));

    [Fact]
    public void Fingerprint_ignores_message_stack_trace_and_time()
    {
        var first = Samples.Error(message: "Order 41 has no shipping address.", stackTrace: "at A", at: Samples.Noon);
        var second = Samples.Error(message: "Order 99 has no shipping address.", stackTrace: "at B", at: Samples.Noon.AddHours(1));

        Assert.Equal(Fingerprint.Of(first), Fingerprint.Of(second));
        Assert.Equal(16, Fingerprint.Of(first).Length);
    }

    [Fact]
    public void Fingerprint_changes_with_service_type_method_route_and_status()
    {
        var fingerprints = new[]
        {
            Samples.Error(),
            Samples.Error(service: "billing-api"),
            Samples.Error(type: "System.TimeoutException"),
            Samples.Error(method: "POST"),
            Samples.Error(route: "/orders/{id}/items"),
            Samples.Error(status: 502),
        }.Select(Fingerprint.Of);

        Assert.Equal(6, fingerprints.Distinct().Count());
    }

    [Fact]
    public void Grouper_counts_occurrences_and_keeps_the_latest_as_sample()
    {
        var events = new[]
        {
            Samples.Error(at: Samples.Noon.AddMinutes(-30), message: "oldest"),
            Samples.Error(at: Samples.Noon, message: "newest"),
            Samples.Error(at: Samples.Noon.AddMinutes(-10), message: "middle"),
            Samples.Error(type: "System.TimeoutException", at: Samples.Noon.AddMinutes(-5)),
        };

        var groups = ErrorGrouper.Group(events);

        Assert.Equal(2, groups.Count);
        Assert.Equal(3, groups[0].Count);
        Assert.Equal(Samples.Noon.AddMinutes(-30), groups[0].FirstSeen);
        Assert.Equal(Samples.Noon, groups[0].LastSeen);
        Assert.Equal("newest", groups[0].Latest.Message);
        Assert.Equal(1, groups[1].Count);
    }
}
