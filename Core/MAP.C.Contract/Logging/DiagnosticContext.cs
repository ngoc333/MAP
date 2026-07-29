namespace MAP.C.Contract.Logging;

public static class DiagnosticContext
{
    public static string SessionId { get; } = Guid.NewGuid().ToString("N");
    private static readonly AsyncLocal<string?> CurrentOperation = new();

    public static string? OperationId => CurrentOperation.Value;

    public static IDisposable BeginOperation(string operationId)
    {
        var previous = CurrentOperation.Value;
        CurrentOperation.Value = operationId;
        return new RestoreOperation(previous);
    }

    private sealed class RestoreOperation(string? previous) : IDisposable
    {
        public void Dispose() => CurrentOperation.Value = previous;
    }
}
