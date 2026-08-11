namespace MAP.C.Contract.Logging;

public static class AppSession
{
    public static string Id { get; } = Guid.NewGuid().ToString("N");
}
