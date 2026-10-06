using System.Net.Http.Headers;
using System.Text;
using LogTriage.Configuration;
using LogTriage.Issues;
using LogTriage.Jira;
using LogTriage.Loki;
using LogTriage.Redaction;
using LogTriage.Sync;
using Microsoft.Extensions.Options;

namespace LogTriage;

public static class ServiceCollectionExtensions
{
    private const string TenantHeader = "X-Scope-OrgID";
    private const int JiraTimeoutSeconds = 30;

    public static IServiceCollection AddLogTriage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LokiOptions>().Bind(configuration.GetSection(LokiOptions.Section))
            .Validate(static loki => IsHttpUrl(loki.Url), "Loki:Url must be an absolute http(s) URL.")
            .Validate(static loki => !string.IsNullOrWhiteSpace(loki.Query), "Loki:Query is required.")
            .Validate(static loki => loki.PageSize is >= 1 and <= 5000, "Loki:PageSize must be between 1 and 5000.")
            .Validate(static loki => loki.MaxEvents >= loki.PageSize, "Loki:MaxEvents must not be lower than Loki:PageSize.")
            .Validate(static loki => loki.TimeoutSeconds >= 1, "Loki:TimeoutSeconds must be at least 1.")
            .ValidateOnStart();

        services.AddOptions<FieldOptions>().Bind(configuration.GetSection(FieldOptions.Section))
            .Validate(static fields => !string.IsNullOrWhiteSpace(fields.ExceptionType), "Fields:ExceptionType is required.")
            .ValidateOnStart();

        services.AddOptions<SyncOptions>().Bind(configuration.GetSection(SyncOptions.Section))
            .Validate(static sync => sync.IntervalSeconds >= 5, "Sync:IntervalSeconds must be at least 5.")
            .Validate(static sync => sync.LookbackMinutes >= 1, "Sync:LookbackMinutes must be at least 1.")
            .Validate(static sync => sync.MinOccurrences >= 1, "Sync:MinOccurrences must be at least 1.")
            .Validate(static sync => sync.MaxIssuesPerRun >= 1, "Sync:MaxIssuesPerRun must be at least 1.")
            .ValidateOnStart();

        services.AddOptions<JiraOptions>().Bind(configuration.GetSection(JiraOptions.Section))
            .Validate<IOptions<SyncOptions>>(
                static (jira, sync) => sync.Value.DryRun || IsComplete(jira),
                "Jira:BaseUrl, Jira:Email, Jira:ApiToken, Jira:ProjectKey and Jira:IssueType are required when Sync:DryRun is false.")
            .Validate(static jira => !jira.FingerprintLabelPrefix.Any(char.IsWhiteSpace), "Jira:FingerprintLabelPrefix must not contain spaces.")
            .ValidateOnStart();

        services.AddOptions<IssueOptions>().Bind(configuration.GetSection(IssueOptions.Section))
            .Validate(static issue => !string.IsNullOrWhiteSpace(issue.TitleTemplate), "Issue:TitleTemplate is required.")
            .Validate(static issue => issue.MaxStackTraceLength >= 1 && issue.MaxRequestBodyLength >= 1, "Issue length limits must be at least 1.")
            .ValidateOnStart();

        services.AddOptions<ApiOptions>().Bind(configuration.GetSection(ApiOptions.Section));

        services.AddHttpClient(LokiClient.ClientName, static (provider, client) =>
        {
            var loki = provider.GetRequiredService<IOptions<LokiOptions>>().Value;
            client.BaseAddress = BaseAddress(loki.Url);
            client.Timeout = TimeSpan.FromSeconds(loki.TimeoutSeconds);
            if (loki.TenantId.Length > 0)
                client.DefaultRequestHeaders.Add(TenantHeader, loki.TenantId);

            if (loki.Username.Length > 0)
                client.DefaultRequestHeaders.Authorization = Basic(loki.Username, loki.Password);
        });

        services.AddHttpClient(JiraIssueTracker.ClientName, static (provider, client) =>
        {
            var jira = provider.GetRequiredService<IOptions<JiraOptions>>().Value;
            client.BaseAddress = BaseAddress(jira.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(JiraTimeoutSeconds);
            client.DefaultRequestHeaders.Authorization = Basic(jira.Email, jira.ApiToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<LokiClient>();
        services.AddSingleton<ErrorEventParser>();
        services.AddSingleton<ErrorReader>();
        services.AddSingleton<RequestRedactor>();
        services.AddSingleton<IssueComposer>();
        services.AddSingleton<IssueRegistry>();
        services.AddSingleton<IIssueTracker, JiraIssueTracker>();
        services.AddSingleton<SyncService>();
        services.AddHostedService<SyncWorker>();
        return services;
    }

    private static bool IsComplete(JiraOptions jira) =>
        IsHttpUrl(jira.BaseUrl)
        && !string.IsNullOrWhiteSpace(jira.Email)
        && !string.IsNullOrWhiteSpace(jira.ApiToken)
        && !string.IsNullOrWhiteSpace(jira.ProjectKey)
        && !string.IsNullOrWhiteSpace(jira.IssueType);

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static Uri? BaseAddress(string url) =>
        IsHttpUrl(url) ? new Uri(url.TrimEnd('/') + "/") : null;

    private static AuthenticationHeaderValue Basic(string user, string secret) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{secret}")));
}
