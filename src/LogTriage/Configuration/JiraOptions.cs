namespace LogTriage.Configuration;

/// <summary>Jira Cloud connection and the fields every new issue gets.</summary>
public sealed class JiraOptions
{
    public const string Section = "Jira";

    public string BaseUrl { get; set; } = "";

    public string Email { get; set; } = "";

    public string ApiToken { get; set; } = "";

    public string ProjectKey { get; set; } = "";

    public string IssueType { get; set; } = "Bug";

    public string AssigneeAccountId { get; set; } = "";

    /// <summary>Key of the epic or parent issue new issues are placed under.</summary>
    public string ParentKey { get; set; } = "";

    /// <summary>Comma-separated labels added to every issue.</summary>
    public string Labels { get; set; } = "logtriage";

    /// <summary>Prefix of the label that carries the group fingerprint. It is what prevents duplicates.</summary>
    public string FingerprintLabelPrefix { get; set; } = "logtriage-";

    /// <summary>Base URL used in issue links. Defaults to <see cref="BaseUrl"/>.</summary>
    public string BrowseUrl { get; set; } = "";
}
