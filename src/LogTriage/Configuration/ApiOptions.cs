namespace LogTriage.Configuration;

/// <summary>The read-only HTTP API.</summary>
public sealed class ApiOptions
{
    public const string Section = "Api";

    /// <summary>Expected in the <c>X-Api-Key</c> header. While it is empty the API answers 404.</summary>
    public string Key { get; set; } = "";
}
