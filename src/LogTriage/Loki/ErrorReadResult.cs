using LogTriage.Grouping;

namespace LogTriage.Loki;

/// <param name="Truncated">True when the window held more lines than <c>Loki:MaxEvents</c>; counts are then lower bounds.</param>
public sealed record ErrorReadResult(IReadOnlyList<ErrorEvent> Events, bool Truncated);
