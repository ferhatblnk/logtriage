namespace LogTriage.Configuration;

/// <summary>What goes into an issue.</summary>
public sealed class IssueOptions
{
    public const string Section = "Issue";

    /// <summary>
    /// Placeholders: <c>{service}</c>, <c>{environment}</c>, <c>{exceptionType}</c>, <c>{exceptionFullType}</c>,
    /// <c>{method}</c>, <c>{route}</c>, <c>{status}</c>, <c>{message}</c>.
    /// </summary>
    public string TitleTemplate { get; set; } = "[{service}] {exceptionType}: {method} {route}";

    /// <summary>Off by default: the query string and body of the latest request are left out of the issue.</summary>
    public bool IncludeRequestData { get; set; }

    /// <summary>Comma-separated words. A request field whose name contains one of them has its value masked.</summary>
    public string SensitiveKeys { get; set; } =
        "password,passwd,pwd,secret,token,authorization,apikey,api_key,api-key,credential,cookie,session,otp,cvv,cvc";

    public int MaxStackTraceLength { get; set; } = 8000;

    public int MaxRequestBodyLength { get; set; } = 4000;
}
