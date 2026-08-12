using System.Text.Json;

namespace MAP.C.Contract.Database;

public static class DbApiClientExtensions
{
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

        if (!element.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Database function {commandName} returned no data array.");

        return data.Deserialize<List<T>>(DbJson.Options)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {commandName} to {typeof(T).Name}.");
    }

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
