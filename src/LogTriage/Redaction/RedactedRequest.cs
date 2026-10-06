namespace LogTriage.Redaction;

/// <param name="Withheld">True when the request looked like it carried payment details; query and body are then empty.</param>
public sealed record RedactedRequest(string Query, string Body, bool Withheld)
{
    public static RedactedRequest Hidden { get; } = new("", "", true);
}
