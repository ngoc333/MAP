using System.Text.Json;
using MAP.C.Contract.Database;
using MAP.C.Contract.Models;

namespace MAP.C.Runtime.Database;

public static class DatabaseMenuLoader
{
    public static async Task<PageConfig> LoadAsync(
        IDbApiClient dbClient,
        string dbName,
        string dbFunction,
        string? programId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dbName))
            throw new InvalidOperationException("Database menu configuration requires DbName.");
        if (string.IsNullOrWhiteSpace(dbFunction))
            throw new InvalidOperationException("Database menu configuration requires DbFunction.");

        var parameters = JsonSerializer.SerializeToElement(new { p_program_id = programId });
        var response = await dbClient.CallPostgreSqlFunctionAsync(
            dbName, dbFunction, parameters, cancellationToken);

        if (!response.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True)
            throw new InvalidOperationException("Database menu API did not return a successful response.");

        var resultPropertyName = dbFunction[(dbFunction.LastIndexOf('.') + 1)..];
        if (!response.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array
            || data.GetArrayLength() == 0
            || data[0].ValueKind != JsonValueKind.Object
            || !data[0].TryGetProperty(resultPropertyName, out var menuJson)
            || menuJson.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(menuJson.GetString()))
        {
            throw new InvalidOperationException("Database menu API returned an invalid menu payload.");
        }

        var config = JsonSerializer.Deserialize<PageConfig>(
            menuJson.GetString()!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (config is null)
            throw new InvalidOperationException("Database menu payload could not be deserialized.");

        return config;
    }
}
