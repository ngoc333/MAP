namespace MAP.M.Template.Models;

/// <summary>Small scalar-only state used to make lifecycle cleanup observable.</summary>
public static class TemplateProbeState
{
    public static int InitializedCount { get; private set; }
    public static int NavigatingCount { get; private set; }
    public static int DisposedCount { get; private set; }
    public static string? LastDisposedInstanceId { get; private set; }

    public static void Initialized() => InitializedCount++;
    public static void Navigating() => NavigatingCount++;
    public static void Disposed(string instanceId)
    {
        DisposedCount++;
        LastDisposedInstanceId = instanceId;
    }

    public static void Reset()
    {
        InitializedCount = 0;
        NavigatingCount = 0;
        DisposedCount = 0;
        LastDisposedInstanceId = null;
    }
}
