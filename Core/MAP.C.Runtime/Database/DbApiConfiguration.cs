using System.Text.Json;

namespace MAP.C.Runtime.Database;

public sealed class DbApiConfiguration
{
    public required Uri OracleBaseAddress { get; init; }
    public required Uri PostgreSqlBaseAddress { get; init; }

    public static DbApiConfiguration LoadFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static DbApiConfiguration Load(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        return Create(document.RootElement);
    }

    public static async Task<DbApiConfiguration> LoadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return Create(document.RootElement);
    }

    private static DbApiConfiguration Create(JsonElement configuration)
    {
        var oracleBaseAddress = GetBaseAddress(configuration, "OracleApiBaseUrl");
        var postgreSqlBaseAddress = GetBaseAddress(configuration, "PostgreSqlApiBaseUrl");

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
