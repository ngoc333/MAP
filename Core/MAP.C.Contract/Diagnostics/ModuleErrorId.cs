namespace MAP.C.Contract.Diagnostics;

/// <summary>
/// Helper for correlating ErrorId between PageNavigator (logging) and
/// MainLayout/BasePage (notification). Attaches ErrorId to Exception.Data
/// so the same ID is used everywhere.
/// </summary>
public static class ModuleErrorId
{
    private const string DataKey = "MAP.ErrorId";

    /// <summary>
    /// Creates a new 8-character uppercase hex ErrorId.
    /// </summary>
    public static string Create()
        => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    /// <summary>
    /// Returns the existing ErrorId from <see cref="Exception.Data"/> if present;
    /// otherwise creates a new one, attaches it, and returns it.
    /// </summary>
    public static string GetOrCreate(Exception ex)
    {
        if (ex.Data[DataKey] is string existing && !string.IsNullOrEmpty(existing))
            return existing;

        var errorId = Create();
        ex.Data[DataKey] = errorId;
        return errorId;
    }

    /// <summary>
    /// Attaches a specific ErrorId to an exception.
    /// </summary>
    public static void Set(Exception ex, string errorId)
    {
        ex.Data[DataKey] = errorId;
    }
}
