using System.Text.Json.Nodes;

namespace SampleApi;

/// <summary>Endpoints that fail on purpose so the demo has errors to report.</summary>
public static class DemoEndpoints
{
    public static IResult GetOrder(int id)
    {
        if (id % 2 == 1)
            throw new InvalidOperationException($"Order {id} has no shipping address.");

        return Results.Ok(new { id, status = "shipped" });
    }

    public static IResult GetInvoice(int id) =>
        throw new KeyNotFoundException($"Invoice for order {id} was not found.");

    public static IResult Login(JsonObject credentials) =>
        throw new FormatException($"Account '{credentials["email"]}' has a malformed profile record.");

    public static IResult Pay(JsonObject payment) =>
        throw new TimeoutException("The payment provider did not answer within 10 seconds.");
}
