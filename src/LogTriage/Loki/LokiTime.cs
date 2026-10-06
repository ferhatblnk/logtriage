namespace LogTriage.Loki;

internal static class LokiTime
{
    private const long NanosecondsPerTick = 100;

    public static long ToNanoseconds(DateTimeOffset time) =>
        (time.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * NanosecondsPerTick;

    public static DateTimeOffset FromNanoseconds(long nanoseconds) =>
        DateTimeOffset.UnixEpoch.AddTicks(nanoseconds / NanosecondsPerTick);
}
