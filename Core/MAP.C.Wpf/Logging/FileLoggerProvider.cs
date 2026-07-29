using MAP.C.Contract.Logging;
using Microsoft.Extensions.Logging;

namespace MAP.C.Wpf.Logging;

public sealed class FileLoggerProvider(FileLogStore store) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, store);
    public void Dispose() { }

    private sealed class FileLogger(string category, FileLogStore store) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            store.Write(new LogEntry
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
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
