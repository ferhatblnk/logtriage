namespace LogTriage.Configuration;

/// <summary>
/// Names of the fields read from each log line. A name is looked up in the JSON body first
/// (flat <c>"exception.type"</c> keys and nested <c>exception → type</c> objects both work),
/// then in the stream labels with every non-alphanumeric character replaced by <c>_</c>.
/// Defaults follow the OpenTelemetry semantic conventions.
/// </summary>
public sealed class FieldOptions
{
    public const string Section = "Fields";

    /// <summary>
    /// Name of the object the fields are nested under, if your logger wraps them in one.
    /// For example <c>State</c> for the JSON console logger of .NET.
    /// </summary>
    public string Root { get; set; } = "";

    public string ExceptionType { get; set; } = "exception.type";

    public string Message { get; set; } = "exception.message";

    public string StackTrace { get; set; } = "exception.stacktrace";

    public string Method { get; set; } = "http.request.method";

    /// <summary>The route template, for example <c>/orders/{id}</c>. Preferred over <see cref="Path"/>.</summary>
    public string Route { get; set; } = "http.route";

    /// <summary>The raw request path. Used to derive a route when no template is logged.</summary>
    public string Path { get; set; } = "url.path";

    public string Query { get; set; } = "url.query";

    public string Body { get; set; } = "http.request.body";

    public string StatusCode { get; set; } = "http.response.status_code";
}
