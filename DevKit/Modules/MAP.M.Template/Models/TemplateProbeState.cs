namespace MAP.M.Template.Models;

/// <summary>Stores the last lifecycle cleanup result so it remains visible after returning to the lab.</summary>
public static class TemplateProbeState
{
    public static string LastCleanup { get; private set; } = "No page cleanup has run yet.";

    public static void RecordCleanup()
    {
        LastCleanup = $"Completed at {DateTimeOffset.Now:T}.";
    }
}
