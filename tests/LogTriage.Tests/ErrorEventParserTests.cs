using LogTriage.Configuration;
using LogTriage.Loki;
using Microsoft.Extensions.Options;

namespace LogTriage.Tests;

public class ErrorEventParserTests
{
    private static readonly Dictionary<string, string> Labels = new() { ["service_name"] = "orders-api", ["env"] = "prod" };

    private readonly ErrorEventParser parser = new(Options.Create(new LokiOptions()), Options.Create(new FieldOptions()));

    [Fact]
    public void Reads_every_field_of_the_default_contract()
    {
        const string line = """
            {"level":"error","exception.type":"System.InvalidOperationException","exception.message":"Order 41 has no shipping address.",
             "exception.stacktrace":"at Orders.Get(Int32 id)","http.request.method":"get","http.route":"/orders/{id:int}","url.path":"/orders/41",
             "url.query":"expand=items","http.request.body":"","http.response.status_code":500}
            """;

        var error = parser.Parse(Entry(line));

        Assert.NotNull(error);
        Assert.Equal("orders-api", error.Service);
        Assert.Equal("prod", error.Environment);
        Assert.Equal("System.InvalidOperationException", error.ExceptionType);
        Assert.Equal("Order 41 has no shipping address.", error.Message);
        Assert.Equal("at Orders.Get(Int32 id)", error.StackTrace);
        Assert.Equal("GET", error.Method);
        Assert.Equal("/orders/{id:int}", error.Route);
        Assert.Equal("/orders/41", error.Path);
        Assert.Equal("expand=items", error.Query);
        Assert.Equal(500, error.StatusCode);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero), error.Timestamp);
    }

    [Fact]
    public void Derives_the_route_from_the_path_when_no_template_is_logged()
    {
        var error = parser.Parse(Entry("""{"exception.type":"System.TimeoutException","url.path":"/orders/41/items?x=1"}"""));

        Assert.Equal("/orders/{id}/items", error?.Route);
        Assert.Null(error?.StatusCode);
    }

    [Fact]
    public void Falls_back_to_stream_labels_with_sanitized_names()
    {
        var labels = new Dictionary<string, string>(Labels)
        {
            ["exception_type"] = "System.TimeoutException",
            ["http_response_status_code"] = "504",
        };

        var error = parser.Parse(new LokiEntry(0, "payment provider timed out", "{}", labels));

        Assert.Equal("System.TimeoutException", error?.ExceptionType);
        Assert.Equal(504, error?.StatusCode);
    }

    [Fact]
    public void Reads_fields_nested_under_a_configured_root_object()
    {
        const string line = """
            {"EventId":0,"LogLevel":"Error","Category":"SampleApi.ErrorLoggingMiddleware","Message":"Unhandled exception: Order 3 has no shipping address.",
             "Exception":"System.InvalidOperationException: Order 3 has no shipping address.",
             "State":{"Message":"Unhandled exception: Order 3 has no shipping address.","exception.type":"System.InvalidOperationException",
                      "http.request.method":"GET","http.route":"/orders/{id:int}","url.path":"/orders/3","http.response.status_code":500}}
            """;
        var nested = new ErrorEventParser(Options.Create(new LokiOptions()), Options.Create(new FieldOptions { Root = "State" }));

        var error = nested.Parse(Entry(line));

        Assert.Equal("System.InvalidOperationException", error?.ExceptionType);
        Assert.Equal("GET", error?.Method);
        Assert.Equal("/orders/{id:int}", error?.Route);
        Assert.Equal(500, error?.StatusCode);
        Assert.Null(parser.Parse(Entry(line)));
    }

    [Theory]
    [InlineData("plain text line")]
    [InlineData("""{"level":"info","message":"request finished"}""")]
    [InlineData("""{"exception.type":"  "}""")]
    [InlineData("""{"exception.type": broken""")]
    public void Skips_lines_that_are_not_errors(string line) =>
        Assert.Null(parser.Parse(Entry(line)));

    [Fact]
    public void Uses_a_placeholder_when_the_service_label_is_missing()
    {
        var error = parser.Parse(new LokiEntry(0, """{"exception.type":"System.Exception"}""", "{}", new Dictionary<string, string>()));

        Assert.Equal("unknown", error?.Service);
        Assert.Equal("", error?.Environment);
    }

    private static LokiEntry Entry(string line) =>
        new(LokiTime.ToNanoseconds(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero)), line, "{}", Labels);
}
