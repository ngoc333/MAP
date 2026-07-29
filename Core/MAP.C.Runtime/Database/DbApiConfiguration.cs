using System.Text.Json;

namespace MAP.C.Runtime.Database;

public sealed class DbApiConfiguration
{
    private const string ResourceName = "MAP.C.Runtime.Database.db-api.json";

    public required Uri OracleBaseAddress { get; init; }
    public required Uri PostgreSqlBaseAddress { get; init; }

    public static DbApiConfiguration Load()
    {
        var assembly = typeof(DbApiConfiguration).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");
        using var document = JsonDocument.Parse(stream);

        var oracleBaseAddress = GetBaseAddress(document.RootElement, "OracleApiBaseUrl");
        var postgreSqlBaseAddress = GetBaseAddress(document.RootElement, "PostgreSqlApiBaseUrl");

        return new DbApiConfiguration
        {
            OracleBaseAddress = oracleBaseAddress,
            PostgreSqlBaseAddress = postgreSqlBaseAddress
        };
    }

    private static Uri GetBaseAddress(JsonElement configuration, string propertyName)
    {
        if (!configuration.TryGetProperty(propertyName, out var baseUrlElement)
            || !Uri.TryCreate(baseUrlElement.GetString(), UriKind.Absolute, out var baseAddress))
        {
            throw new InvalidOperationException($"Database API configuration requires an absolute {propertyName}.");
        }

        return baseAddress;
    }
}
