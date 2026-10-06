namespace LogTriage.Configuration;

/// <summary>Where the error logs live and how to select them.</summary>
public sealed class LokiOptions
{
    public const string Section = "Loki";

    public string Url { get; set; } = "http://localhost:3100";

    /// <summary>Sent as <c>X-Scope-OrgID</c> when Loki runs in multi-tenant mode.</summary>
    public string TenantId { get; set; } = "";

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    /// <summary>A LogQL log query that returns the error lines. Metric queries are not supported.</summary>
    public string Query { get; set; } = "{service_name=~`.+`} |= `exception.type`";

    public string ServiceLabel { get; set; } = "service_name";

    public string EnvironmentLabel { get; set; } = "env";

    public int PageSize { get; set; } = 1000;

    /// <summary>Upper bound on the lines read in one pass. Results beyond it are reported as truncated.</summary>
    public int MaxEvents { get; set; } = 20000;

    public int TimeoutSeconds { get; set; } = 30;
}
