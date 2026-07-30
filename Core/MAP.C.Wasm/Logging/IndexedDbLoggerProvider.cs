using MAP.C.Contract.Logging;
using Microsoft.Extensions.Logging;

namespace MAP.C.Wasm.Logging;

public sealed class IndexedDbLoggerProvider(ILogStore store) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new IndexedDbLogger(categoryName, store);
    public void Dispose() { }

    private sealed class IndexedDbLogger(string category, ILogStore store) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            _ = WriteAsync(new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = logLevel.ToString(),
                Category = category,
                EventName = eventId.Name ?? string.Empty,
                Message = formatter(state, exception),
                Exception = exception?.ToString(),
                SessionId = DiagnosticContext.SessionId,
                OperationId = DiagnosticContext.OperationId
            });
        }

        private async Task WriteAsync(LogEntry entry)
        {
            try { await store.WriteAsync(entry); }
            catch { /* Logging must never terminate application flow. */ }
        }
    }
}
