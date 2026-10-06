using LogTriage.Grouping;

namespace LogTriage.Tests.Support;

internal static class Samples
{
    public static readonly DateTimeOffset Noon = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public static ErrorEvent Error(
        string service = "orders-api",
        string type = "System.InvalidOperationException",
        string method = "GET",
        string route = "/orders/{id}",
        int? status = 500,
        DateTimeOffset? at = null,
        string message = "Order 41 has no shipping address.",
        string stackTrace = "   at Orders.Get(Int32 id)",
        string path = "/orders/41",
        string query = "",
        string body = "") =>
        new(at ?? Noon, service, "demo", type, message, stackTrace, method, route, path, query, body, status);

    public static ErrorGroup Group(int count = 3, int? status = 500, DateTimeOffset? lastSeen = null, string route = "/orders/{id}", string query = "", string body = "")
    {
        var last = lastSeen ?? Noon;
        var events = Enumerable.Range(0, count).Select(index => Error(route: route, status: status, at: last.AddMinutes(-index), query: query, body: body));
        return ErrorGrouper.Group(events).Single();
    }
}
