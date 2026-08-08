using System.Text.Json;
using MAP.C.Contract.Database;
using Microsoft.Extensions.Logging;

namespace MAP.C.Wasm.Database;

/// <summary>
/// Fallback implementation used when db-api.json cannot be loaded.
/// Every DB operation fails immediately with a clear diagnostic instead
/// of returning a fake empty response.
/// </summary>
internal sealed class FallbackDbApiClient : IDbApiClient
{
    private readonly ILogger _logger;
    private readonly string _reason;

    public FallbackDbApiClient(ILogger logger, string reason)
    {
        _logger = logger;
        _reason = reason;
    }

    public Task<JsonElement> CallOracleAsync(JsonElement requestBody, CancellationToken cancellationToken = default)
    {
        _logger.LogError("DB API unavailable (Oracle). Reason={Reason}", _reason);
        return Task.FromException<JsonElement>(
            new InvalidOperationException($"Database API is not configured: {_reason}"));
    }

    public Task<JsonElement> CallPostgreSqlFunctionAsync(string dbName, string commandName, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        _logger.LogError("DB API unavailable (PostgreSQL function). Reason={Reason}", _reason);
        return Task.FromException<JsonElement>(
            new InvalidOperationException($"Database API is not configured: {_reason}"));
    }

    public Task<JsonElement> CallPostgreSqlProcedureAsync(string dbName, string commandName, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        _logger.LogError("DB API unavailable (PostgreSQL procedure). Reason={Reason}", _reason);
        return Task.FromException<JsonElement>(
            new InvalidOperationException($"Database API is not configured: {_reason}"));
    }
}
