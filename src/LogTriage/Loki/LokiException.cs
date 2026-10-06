namespace LogTriage.Loki;

public sealed class LokiException(string message) : Exception(message);
