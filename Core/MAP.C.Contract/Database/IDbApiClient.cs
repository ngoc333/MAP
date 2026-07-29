using System.Text.Json;

namespace MAP.C.Contract.Database;

public interface IDbApiClient
{
    Task<JsonElement> CallOracleAsync(JsonElement requestBody, CancellationToken cancellationToken = default);
    Task<JsonElement> CallPostgreSqlFunctionAsync(string dbName, string commandName, JsonElement parameters, CancellationToken cancellationToken = default);
    Task<JsonElement> CallPostgreSqlProcedureAsync(string dbName, string commandName, JsonElement parameters, CancellationToken cancellationToken = default);
}
