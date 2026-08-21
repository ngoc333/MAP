using System.Text.Json;

namespace MAP.C.Contract.Database;

/// <summary>Convenience methods for PostgreSQL database API calls.</summary>
public static class DbApiClientExtensions
{
    /// <summary>Calls a PostgreSQL function, validates the response, and returns the raw JSON response.</summary>
    public static async Task<JsonElement> QueryPostgreSqlFunctionAsync(
        this IDbApiClient client,
        string dbName,
        string commandName,
        object parameters,
        CancellationToken cancellationToken = default)
    {
        var response = await client.CallPostgreSqlFunctionAsync(
            dbName, commandName, DbJson.ToElement(parameters), cancellationToken);

        ValidateResponse(response, commandName);
        return response;
    }

    /// <summary>Calls a PostgreSQL function and maps its array response data to a list.</summary>
    public static async Task<List<T>> QueryPostgreSqlFunctionAsync<T>(
        this IDbApiClient client,
        string dbName,
        string commandName,
        object parameters,
        CancellationToken cancellationToken = default)
    {
        var response = await client.QueryPostgreSqlFunctionAsync(
            dbName, commandName, parameters, cancellationToken);

        var data = GetData(response, commandName);
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

    /// <summary>Calls a PostgreSQL function and maps its response data to a single model.</summary>
    public static async Task<T?> QuerySinglePostgreSqlFunctionAsync<T>(
        this IDbApiClient client,
        string dbName,
        string commandName,
        object parameters,
        CancellationToken cancellationToken = default)
    {
        var response = await client.QueryPostgreSqlFunctionAsync(
            dbName, commandName, parameters, cancellationToken);

        var data = GetData(response, commandName);
        if (data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return default;

        try
        {
            return data.Deserialize<T>(DbJson.Options);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Failed to map database function {commandName} to {typeof(T).Name}.",
                exception);
        }
    }

    /// <summary>Calls a PostgreSQL procedure, validates the response, and returns the raw JSON response.</summary>
    public static async Task<JsonElement> ExecutePostgreSqlProcedureAsync(
        this IDbApiClient client,
        string dbName,
        string commandName,
        object parameters,
        CancellationToken cancellationToken = default)
    {
        var response = await client.CallPostgreSqlProcedureAsync(
            dbName, commandName, DbJson.ToElement(parameters), cancellationToken);

        ValidateResponse(response, commandName);
        return response;
    }

    private static JsonElement GetData(JsonElement response, string commandName)
    {
        if (!response.TryGetProperty("data", out var data))
            throw new InvalidOperationException($"Database function {commandName} returned no data.");

        return data;
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
