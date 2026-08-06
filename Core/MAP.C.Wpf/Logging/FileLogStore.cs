using System.Text.Json;
using System.IO;
using MAP.C.Contract.Logging;

namespace MAP.C.Wpf.Logging;

public sealed class FileLogStore : ILogStore
{
    private const int RetentionDays = 30;
    private readonly string _logDirectory = Path.Combine(AppContext.BaseDirectory, "log");
    private readonly Lock _writeLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public FileLogStore()
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            DeleteExpiredFiles();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileLogStore] Failed to initialize log directory: {ex.Message}");
        }
    }

    public Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            lock (_writeLock)
            {
                Directory.CreateDirectory(_logDirectory);
                var path = Path.Combine(_logDirectory, $"{entry.Timestamp:yyyy-MM-dd}.log");
                File.AppendAllText(path, JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileLogStore] Write failed: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<LogEntry>> GetAsync(DateOnly? day = null, CancellationToken cancellationToken = default)
    {
        var days = day is null ? await GetDaysAsync(cancellationToken) : [day.Value];
        var entries = new List<LogEntry>();
        foreach (var item in days)
        {
            var path = Path.Combine(_logDirectory, $"{item:yyyy-MM-dd}.log");
            if (!File.Exists(path)) continue;

            var malformedCount = 0;
            using var reader = new StreamReader(path);
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<LogEntry>(line, JsonOptions);
                    if (entry is not null) entries.Add(entry);
                }
                catch (JsonException)
                {
                    malformedCount++;
                }
            }

            if (malformedCount > 0)
                System.Diagnostics.Debug.WriteLine($"[FileLogStore] Skipped {malformedCount} malformed log lines in {path}");
        }
        return entries.OrderByDescending(x => x.Timestamp).ToList();
    }

    public Task<IReadOnlyList<DateOnly>> GetDaysAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<DateOnly> days = Directory.Exists(_logDirectory)
            ? Directory.EnumerateFiles(_logDirectory, "????-??-??.log")
                .Select(Path.GetFileNameWithoutExtension)
                .Select(x => DateOnly.TryParseExact(x, "yyyy-MM-dd", out var day) ? day : (DateOnly?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .OrderByDescending(x => x)
            : [];
        return Task.FromResult<IReadOnlyList<DateOnly>>(days.ToList());
    }

    public Task ClearAsync(DateOnly? day = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (day is not null)
            {
                var path = Path.Combine(_logDirectory, $"{day:yyyy-MM-dd}.log");
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                // Clear all log files
                if (Directory.Exists(_logDirectory))
                {
                    foreach (var file in Directory.EnumerateFiles(_logDirectory, "????-??-??.log"))
                    {
                        File.Delete(file);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileLogStore] Clear failed: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    private void DeleteExpiredFiles()
    {
        if (!Directory.Exists(_logDirectory)) return;

        var cutoff = DateOnly.FromDateTime(DateTime.Today.AddDays(-RetentionDays));
        var files = Directory.EnumerateFiles(_logDirectory, "????-??-??.log")
            .Select(f => new
            {
                Path = f,
                Day = DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(f), "yyyy-MM-dd", out var d) ? d : (DateOnly?)null
            })
            .Where(x => x.Day.HasValue && x.Day.Value < cutoff);

        foreach (var file in files)
        {
            try { File.Delete(file.Path); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FileLogStore] Failed to delete expired log: {ex.Message}"); }
        }
    }
}
