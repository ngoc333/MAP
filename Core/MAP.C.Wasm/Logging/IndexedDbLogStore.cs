using MAP.C.Contract.Logging;
using Microsoft.JSInterop;

namespace MAP.C.Wasm.Logging;

public sealed class IndexedDbLogStore(IJSRuntime js) : ILogStore
{
    public async Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        await js.InvokeVoidAsync("mapLog.write", cancellationToken, entry);
    }

    public async Task<IReadOnlyList<LogEntry>> GetAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var result = await js.InvokeAsync<LogEntry[]>("mapLog.get", cancellationToken, day.ToString("yyyy-MM-dd"));
        return result;
    }

    public async Task<IReadOnlyList<DateOnly>> GetDaysAsync(CancellationToken cancellationToken = default)
    {
        var result = await js.InvokeAsync<string[]>("mapLog.days", cancellationToken);
        return result.Select(DateOnly.Parse).ToList();
    }

    public Task ClearAsync(DateOnly day, CancellationToken cancellationToken = default) =>
        js.InvokeVoidAsync("mapLog.clear", cancellationToken, day.ToString("yyyy-MM-dd")).AsTask();
}
