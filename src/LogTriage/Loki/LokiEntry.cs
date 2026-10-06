namespace LogTriage.Loki;

/// <summary>One log line as Loki returned it.</summary>
/// <param name="Stream">The raw label set of the stream, used to tell identical lines of different streams apart.</param>
public sealed record LokiEntry(long TimestampNs, string Line, string Stream, IReadOnlyDictionary<string, string> Labels);
