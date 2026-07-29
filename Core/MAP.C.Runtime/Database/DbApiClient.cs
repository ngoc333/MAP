using System.Net;
using System.Text;
using System.Text.Json;
using MAP.C.Contract.Database;

namespace MAP.C.Runtime.Database;

public sealed class DbApiClient(HttpClient oracleHttp, HttpClient postgreSqlHttp) : IDbApiClient
{
    private readonly HttpClient _oracleHttp = oracleHttp ?? throw new ArgumentNullException(nameof(oracleHttp));
    private readonly HttpClient _postgreSqlHttp = postgreSqlHttp ?? throw new ArgumentNullException(nameof(postgreSqlHttp));

    public async Task<JsonElement> CallOracleAsync(JsonElement requestBody, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(requestBody.GetRawText(), Encoding.UTF8, "application/json");
        using var response = await _oracleHttp.PostAsync("/api/call-procedure", content, cancellationToken);
        return await HandleResponseAsync(response, cancellationToken);
    }

    public Task<JsonElement> CallPostgreSqlFunctionAsync(string dbName, string commandName, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        var uri = $"/db/pg/call/{Uri.EscapeDataString(dbName)}/{Uri.EscapeDataString(commandName)}";
        return CallPostgreSqlAsync(uri, parameters, cancellationToken);
    }

    public Task<JsonElement> CallPostgreSqlProcedureAsync(string dbName, string commandName, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        var uri = $"/db/pg/call/proc/{Uri.EscapeDataString(dbName)}/{Uri.EscapeDataString(commandName)}";
        return CallPostgreSqlAsync(uri, parameters, cancellationToken);
    }

    private async Task<JsonElement> CallPostgreSqlAsync(string uri, JsonElement parameters, CancellationToken cancellationToken)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("PostgreSQL parameters must be a JSON object.", nameof(parameters));

        var body = JsonSerializer.Serialize(new { @params = parameters });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _postgreSqlHttp.PostAsync(uri, content, cancellationToken);
        return await HandleResponseAsync(response, cancellationToken);
    }

    private static async Task<JsonElement> HandleResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"{(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);
        }

        return JsonSerializer.Deserialize<JsonElement>(body);
    }
}
