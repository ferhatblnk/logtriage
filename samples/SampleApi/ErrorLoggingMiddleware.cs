using Microsoft.AspNetCore.Routing;

namespace SampleApi;

/// <summary>
/// Logs every unhandled exception as one structured record whose field names follow the
/// OpenTelemetry semantic conventions. This record is the whole contract with LogTriage:
/// any logging library that ships these fields to Loki as JSON works.
/// </summary>
public sealed class ErrorLoggingMiddleware(RequestDelegate next, ILogger<ErrorLoggingMiddleware> logger)
{
    private const int BodyLimit = 4096;

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.RequestAborted.IsCancellationRequested)
        {
            var status = StatusOf(exception);
            var fields = new List<KeyValuePair<string, object?>>
            {
                new("exception.type", exception.GetType().FullName),
                new("exception.message", exception.Message),
                new("exception.stacktrace", exception.ToString()),
                new("http.request.method", context.Request.Method),
                new("http.route", (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText),
                new("url.path", context.Request.Path.Value),
                new("url.query", context.Request.QueryString.Value?.TrimStart('?')),
                new("http.request.body", await ReadBodyAsync(context.Request)),
                new("http.response.status_code", status),
            };

            logger.Log(LogLevel.Error, default, fields, exception, static (_, error) => $"Unhandled exception: {error?.Message}");

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = status;
                await context.Response.WriteAsJsonAsync(new { error = "The request could not be completed." });
            }
        }
    }

    private static int StatusOf(Exception exception) => exception switch
    {
        BadHttpRequestException badRequest => badRequest.StatusCode,
        KeyNotFoundException => StatusCodes.Status404NotFound,
        ArgumentException => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var buffer = new char[BodyLimit];
        var length = await reader.ReadBlockAsync(buffer);
        return new string(buffer, 0, length);
    }
}
