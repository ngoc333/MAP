using System.Text.Json;

namespace MAP.C.Contract.Database;

/// <summary>Typed convenience methods for PostgreSQL database API calls.</summary>
public static class DbApiClientExtensions
{
    /// <summary>Calls a PostgreSQL function and maps its array response to a list.</summary>
    public static async Task<List<T>> QueryPostgreSqlFunctionAsync<T>(
        this IDbApiClient client,
        string dbName,
        string commandName,
        object parameters,
        CancellationToken cancellationToken = default)
    {
        var element = await client.CallPostgreSqlFunctionAsync(
            dbName, commandName, DbJson.ToElement(parameters), cancellationToken);

        ValidateResponse(element, commandName);

        if (!element.TryGetProperty("data", out var data))
            throw new InvalidOperationException($"Database function {commandName} returned no data.");

        if (data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Database function {commandName} returned data that is not an array.");

        try
        {
            return data.Deserialize<List<T>>(DbJson.Options) ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Failed to map database function {commandName} to {typeof(T).Name}.",
                exception);
        }
    }

    /// <summary>Calls a PostgreSQL procedure and validates its response.</summary>
    public static async Task ExecutePostgreSqlProcedureAsync(
        this IDbApiClient client,
        string dbName,
        string commandName,
        object parameters,
        CancellationToken cancellationToken = default)
    {
        var element = await client.CallPostgreSqlProcedureAsync(
            dbName, commandName, DbJson.ToElement(parameters), cancellationToken);

        ValidateResponse(element, commandName);
    }

    private static void ValidateResponse(JsonElement response, string commandName)
    {
        if (!response.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True)
        {
            var message = response.TryGetProperty("message", out var msg) ? msg.GetString() : null;
            throw new InvalidOperationException(
                message ?? $"Database request {commandName} failed.");
        }
    }
}
