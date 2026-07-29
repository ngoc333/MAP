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
        Directory.CreateDirectory(_logDirectory);
        DeleteExpiredFiles();
    }

    public void Write(LogEntry entry)
    {
        lock (_writeLock)
        {
            Directory.CreateDirectory(_logDirectory);
            var path = Path.Combine(_logDirectory, $"{entry.Timestamp:yyyy-MM-dd}.log");
            File.AppendAllText(path, JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine);
            DeleteExpiredFiles();
        }
    }

    public async Task<IReadOnlyList<LogEntry>> GetAsync(DateOnly? day = null, CancellationToken cancellationToken = default)
    {
        var days = day is null ? await GetDaysAsync(cancellationToken) : [day.Value];
        var entries = new List<LogEntry>();
        foreach (var item in days)
        {
            var path = Path.Combine(_logDirectory, $"{item:yyyy-MM-dd}.log");
            if (!File.Exists(path)) continue;
            var lines = await File.ReadAllLinesAsync(path, cancellationToken);
            foreach (var line in lines)
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<LogEntry>(line, JsonOptions);
                    if (entry is not null) entries.Add(entry);
                }
                catch (JsonException) { }
            }
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
        if (day is not null)
        {
            var path = Path.Combine(_logDirectory, $"{day:yyyy-MM-dd}.log");
            if (File.Exists(path)) File.Delete(path);
        }
        return Task.CompletedTask;
    }

    private void DeleteExpiredFiles()
    {
        var files = Directory.EnumerateFiles(_logDirectory, "????-??-??.log")
            .OrderByDescending(File.GetCreationTimeUtc)
            .Skip(RetentionDays);
        foreach (var file in files) File.Delete(file);
    }
}
