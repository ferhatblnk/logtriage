using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;

namespace SampleApi;

/// <summary>
/// Demo only. Pushes each log record to Loki as a JSON line so the sample runs without a log shipper.
/// In a real service keep the shipper you already have (Alloy, Promtail, a Loki sink for your logging library).
/// </summary>
public sealed class DemoLokiLoggerProvider : ILoggerProvider
{
    private const int QueueSize = 1000;
    private const int BatchSize = 100;

    private readonly Channel<LogLine> queue = Channel.CreateBounded<LogLine>(new BoundedChannelOptions(QueueSize) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly Dictionary<string, string> labels;
    private readonly Uri pushUrl;

    public DemoLokiLoggerProvider(IConfiguration configuration)
    {
        pushUrl = new Uri($"{configuration["Loki:Url"]?.TrimEnd('/')}/loki/api/v1/push");
        labels = new Dictionary<string, string>
        {
            ["service_name"] = configuration["Loki:ServiceName"] ?? "orders-api",
            ["env"] = configuration["Loki:Environment"] ?? "demo",
        };

        _ = Task.Run(PumpAsync);
    }

    public ILogger CreateLogger(string categoryName) => new DemoLokiLogger(categoryName, queue.Writer);

    public void Dispose()
    {
        queue.Writer.TryComplete();
        http.Dispose();
    }

    private async Task PumpAsync()
    {
        var batch = new List<LogLine>(BatchSize);
        while (await queue.Reader.WaitToReadAsync())
        {
            while (batch.Count < BatchSize && queue.Reader.TryRead(out var line))
                batch.Add(line);

            var values = batch.Select(static line => new[] { line.TimestampNs.ToString(CultureInfo.InvariantCulture), line.Json });
            try
            {
                using var response = await http.PostAsJsonAsync(pushUrl, new { streams = new[] { new { stream = labels, values } } });
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ObjectDisposedException)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            batch.Clear();
        }
    }

    private sealed record LogLine(long TimestampNs, string Json);

    private sealed class DemoLokiLogger(string category, ChannelWriter<LogLine> writer) : ILogger
    {
        private const string TemplateKey = "{OriginalFormat}";

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var now = DateTimeOffset.UtcNow;
            var record = new Dictionary<string, object?>
            {
                ["timestamp"] = now,
                ["level"] = logLevel.ToString().ToLowerInvariant(),
                ["category"] = category,
                ["message"] = formatter(state, exception),
            };

            if (state is IEnumerable<KeyValuePair<string, object?>> fields)
            {
                foreach (var field in fields)
                {
                    if (field.Key != TemplateKey)
                        record[field.Key] = field.Value is null or string or int or long or bool or double ? field.Value : field.Value.ToString();
                }
            }

            var nanoseconds = (now.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100;
            writer.TryWrite(new LogLine(nanoseconds, JsonSerializer.Serialize(record)));
        }
    }
}
