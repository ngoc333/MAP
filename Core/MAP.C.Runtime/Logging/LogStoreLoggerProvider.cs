using MAP.C.Contract.Logging;
using Microsoft.Extensions.Logging;

namespace MAP.C.Runtime.Logging;

/// <summary>
/// Platform-agnostic logger provider that delegates to <see cref="ILogStore"/>.
/// Replaces the previous FileLoggerProvider (WPF) and IndexedDbLoggerProvider (Wasm).
/// </summary>
public sealed class LogStoreLoggerProvider(ILogStore store) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new LogStoreLogger(categoryName, store);
    public void Dispose() { }

    private sealed class LogStoreLogger(string category, ILogStore store) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            _ = WriteAsync(new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = logLevel.ToString(),
                Category = category,
                Message = formatter(state, exception),
                Exception = exception?.ToString(),
                SessionId = AppSession.Id
            });
        }

        private async Task WriteAsync(LogEntry entry)
        {
            try { await store.WriteAsync(entry); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LogStoreLogger] Write failed: {ex.Message}"); }
        }
    }

}
