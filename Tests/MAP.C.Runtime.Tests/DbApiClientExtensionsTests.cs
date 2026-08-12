using System.Text.Json;
using MAP.C.Contract.Database;

namespace MAP.C.Runtime.Tests;

public sealed class DbApiClientExtensionsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static IDbApiClient CreateClient(string responseJson) =>
        new FakeDbApiClient(JsonSerializer.Deserialize<JsonElement>(responseJson));

    private static IDbApiClient CreateClient(JsonElement response) =>
        new FakeDbApiClient(response);

    [Fact]
    public async Task QueryPostgreSqlFunctionAsync_MapsSnakeCaseToPascalCase()
    {
        var response = JsonSerializer.SerializeToElement(new
        {
            success = true,
            data = new[]
            {
                new { page_id = "TEST", title_vi = "Kiểm thử", title_en = "Test", is_active = true }
            }
        }, JsonOptions);

        var client = CreateClient(response);
        var result = await client.QueryPostgreSqlFunctionAsync<TestPage>("db", "func", new { });

        Assert.Single(result);
        Assert.Equal("TEST", result[0].PageId);
        Assert.Equal("Kiểm thử", result[0].TitleVi);
        Assert.Equal("Test", result[0].TitleEn);
        Assert.True(result[0].IsActive);
    }

    [Fact]
    public async Task QueryPostgreSqlFunctionAsync_SendsPascalCaseAsSnakeCase()
    {
        var fakeClient = new FakeDbApiClient(JsonSerializer.SerializeToElement(new
        {
            success = true,
            data = Array.Empty<object>()
        }, JsonOptions));

        await fakeClient.QueryPostgreSqlFunctionAsync<object>("db", "func",
            new { PSearch = "abc", PIncludeInactive = true, PNote = (string?)null });

        var sentParams = fakeClient.LastParameters!.Value;
        Assert.True(sentParams.TryGetProperty("p_search", out var search));
        Assert.Equal("abc", search.GetString());
        Assert.True(sentParams.TryGetProperty("p_include_inactive", out var inactive));
        Assert.True(inactive.GetBoolean());
        Assert.True(sentParams.TryGetProperty("p_note", out var note));
        Assert.Equal(JsonValueKind.Null, note.ValueKind);
    }

    [Fact]
    public async Task QueryPostgreSqlFunctionAsync_NestedBulkMapping()
    {
        var fakeClient = new FakeDbApiClient(JsonSerializer.SerializeToElement(new
        {
            success = true,
            data = Array.Empty<object>()
        }, JsonOptions));

        var pages = new[]
        {
            new { PageId = "P1", TitleVi = "Page 1" },
            new { PageId = "P2", TitleVi = "Page 2" }
        };

        await fakeClient.QueryPostgreSqlFunctionAsync<object>("db", "func",
            new { PRows = pages, PUserName = "NGOC", PIpAddress = "127.0.0.1" });

        var sentParams = fakeClient.LastParameters!.Value;
        Assert.True(sentParams.TryGetProperty("p_rows", out var rows));
        Assert.Equal(JsonValueKind.Array, rows.ValueKind);
        Assert.Equal(2, rows.GetArrayLength());

        var first = rows[0];
        Assert.True(first.TryGetProperty("page_id", out var pid));
        Assert.Equal("P1", pid.GetString());
        Assert.True(first.TryGetProperty("title_vi", out var tvi));
        Assert.Equal("Page 1", tvi.GetString());

        Assert.True(sentParams.TryGetProperty("p_user_name", out var user));
        Assert.Equal("NGOC", user.GetString());
        Assert.True(sentParams.TryGetProperty("p_ip_address", out var ip));
        Assert.Equal("127.0.0.1", ip.GetString());
    }

    [Fact]
    public async Task QueryPostgreSqlFunctionAsync_SuccessFalse_Throws()
    {
        var response = JsonSerializer.SerializeToElement(new
        {
            success = false,
            message = "Custom error"
        }, JsonOptions);

        var client = CreateClient(response);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.QueryPostgreSqlFunctionAsync<object>("db", "func", new { }));
        Assert.Contains("Custom error", ex.Message);
    }

    [Fact]
    public async Task QueryPostgreSqlFunctionAsync_NoData_Throws()
    {
        var response = JsonSerializer.SerializeToElement(new
        {
            success = true
        }, JsonOptions);

        var client = CreateClient(response);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.QueryPostgreSqlFunctionAsync<object>("db", "func", new { }));
        Assert.Contains("no data", ex.Message);
    }

    [Fact]
    public async Task QueryPostgreSqlFunctionAsync_DataNotArray_Throws()
    {
        var response = JsonSerializer.SerializeToElement(new
        {
            success = true,
            data = "not an array"
        }, JsonOptions);

        var client = CreateClient(response);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.QueryPostgreSqlFunctionAsync<object>("db", "func", new { }));
        Assert.Contains("not an array", ex.Message);
    }

    [Fact]
    public async Task QueryPostgreSqlFunctionAsync_InvalidModelMapping_ThrowsWithFunctionAndModel()
    {
        var response = JsonSerializer.SerializeToElement(new
        {
            success = true,
            data = new[] { new { page_id = 42 } }
        }, JsonOptions);

        var client = CreateClient(response);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.QueryPostgreSqlFunctionAsync<TestPage>("db", "mes.map_page_list_f", new { }));

        Assert.Contains("mes.map_page_list_f", exception.Message);
        Assert.Contains(nameof(TestPage), exception.Message);
        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task ExecutePostgreSqlProcedureAsync_SuccessTrue_DoesNotThrow()
    {
        var response = JsonSerializer.SerializeToElement(new
        {
            success = true
        }, JsonOptions);

        var client = CreateClient(response);
        await client.ExecutePostgreSqlProcedureAsync("db", "proc", new { });
    }

    [Fact]
    public async Task ExecutePostgreSqlProcedureAsync_SuccessFalse_Throws()
    {
        var response = JsonSerializer.SerializeToElement(new
        {
            success = false,
            message = "Procedure failed"
        }, JsonOptions);

        var client = CreateClient(response);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ExecutePostgreSqlProcedureAsync("db", "proc", new { }));
        Assert.Contains("Procedure failed", ex.Message);
    }

    [Fact]
    public async Task QueryPostgreSqlFunctionAsync_Cancellation_Throws()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var response = JsonSerializer.SerializeToElement(new
        {
            success = true,
            data = Array.Empty<object>()
        }, JsonOptions);

        var client = CreateClient(response);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.QueryPostgreSqlFunctionAsync<object>("db", "func", new { }, cts.Token));
    }

    private sealed class TestPage
    {
        public string PageId { get; set; } = "";
        public string TitleVi { get; set; } = "";
        public string? TitleEn { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class FakeDbApiClient : IDbApiClient
    {
        private readonly JsonElement _response;
        public JsonElement? LastParameters { get; private set; }

        public FakeDbApiClient(JsonElement response) => _response = response;

        public Task<JsonElement> CallOracleAsync(JsonElement requestBody, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastParameters = requestBody;
            return Task.FromResult(_response);
        }

        public Task<JsonElement> CallPostgreSqlFunctionAsync(string dbName, string commandName, JsonElement parameters, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastParameters = parameters;
            return Task.FromResult(_response);
        }

        public Task<JsonElement> CallPostgreSqlProcedureAsync(string dbName, string commandName, JsonElement parameters, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastParameters = parameters;
            return Task.FromResult(_response);
        }
    }
}
