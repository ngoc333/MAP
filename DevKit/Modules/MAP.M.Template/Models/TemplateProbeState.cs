namespace MAP.M.Template.Models;

/// <summary>Small scalar-only state used to make navigation and lifecycle behavior observable across page instances.</summary>
public static class TemplateProbeState
{
    private const int MaxHistoryLength = 480;

    public static int InitializedCount { get; private set; }
    public static int NavigatingCount { get; private set; }
    public static int ChangedCount { get; private set; }
    public static int CancellationObservedCount { get; private set; }
    public static int DisposedCount { get; private set; }
    public static string? LastInitializedInstanceId { get; private set; }
    public static string? LastDisposedInstanceId { get; private set; }
    public static string? LastNavigatedFrom { get; private set; }
    public static string? LastNavigatedTo { get; private set; }
    public static string NavigationHistory { get; private set; } = string.Empty;

    public static void Initialized(string instanceId)
    {
        InitializedCount++;
        LastInitializedInstanceId = instanceId;
    }

    public static void Navigating(string? fromPageId, string? toPageId)
    {
        NavigatingCount++;
        LastNavigatedFrom = fromPageId;
        LastNavigatedTo = toPageId;
        AppendHistory($"{fromPageId ?? "?"}>NAVIGATING>{toPageId ?? "?"}");
    }

    public static void Changed(string? pageId)
    {
        ChangedCount++;
        AppendHistory($"{pageId ?? "?"}>OPEN");
    }

    public static void CancellationObserved() => CancellationObservedCount++;

    public static void Disposed(string instanceId)
    {
        DisposedCount++;
        LastDisposedInstanceId = instanceId;
    }

    public static void Reset()
    {
        InitializedCount = 0;
        NavigatingCount = 0;
        ChangedCount = 0;
        CancellationObservedCount = 0;
        DisposedCount = 0;
        LastInitializedInstanceId = null;
        LastDisposedInstanceId = null;
        LastNavigatedFrom = null;
        LastNavigatedTo = null;
        NavigationHistory = string.Empty;
    }

    private static void AppendHistory(string entry)
    {
        var history = string.IsNullOrEmpty(NavigationHistory) ? entry : $"{NavigationHistory} → {entry}";
        NavigationHistory = history.Length <= MaxHistoryLength ? history : history[^MaxHistoryLength..];
    }
}
