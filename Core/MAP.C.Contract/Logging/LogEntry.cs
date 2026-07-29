namespace MAP.C.Contract.Logging;

public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; init; }
    public string Level { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string EventName { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Exception { get; init; }
    public string? SessionId { get; init; }
    public string? OperationId { get; init; }
}
