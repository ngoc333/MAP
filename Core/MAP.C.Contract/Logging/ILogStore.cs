namespace MAP.C.Contract.Logging;

public interface ILogStore
{
    Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LogEntry>> GetAsync(DateOnly? day = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DateOnly>> GetDaysAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(DateOnly? day = null, CancellationToken cancellationToken = default);
}
