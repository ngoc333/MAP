using System.Text.Json;

namespace MAP.C.Contract.Database;

/// <summary>Low-level client for configured MAP database API endpoints.</summary>
public interface IDbApiClient
{
    /// <summary>Calls an Oracle endpoint with its protocol request body.</summary>
    Task<JsonElement> CallOracleAsync(JsonElement requestBody, CancellationToken cancellationToken = default);

    /// <summary>Calls a PostgreSQL function in the specified database.</summary>
    Task<JsonElement> CallPostgreSqlFunctionAsync(string dbName, string commandName, JsonElement parameters, CancellationToken cancellationToken = default);

    /// <summary>Calls a PostgreSQL procedure in the specified database.</summary>
    Task<JsonElement> CallPostgreSqlProcedureAsync(string dbName, string commandName, JsonElement parameters, CancellationToken cancellationToken = default);
}
