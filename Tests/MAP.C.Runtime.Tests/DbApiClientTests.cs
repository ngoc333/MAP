using System.Net;
using System.Text;
using System.Text.Json;
using MAP.C.Runtime.Database;

namespace MAP.C.Runtime.Tests;

public sealed class DbApiClientTests
{
    private static DbApiClient CreateClient(HttpResponseMessage response)
    {
        var handler = new TestHttpMessageHandler(response);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.example/") };
        return new DbApiClient(httpClient, httpClient);
    }

    private static DbApiClient CreateClient(Exception exception)
    {
        var handler = new TestHttpMessageHandler(exception);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.example/") };
        return new DbApiClient(httpClient, httpClient);
    }

    [Fact]
    public async Task CallOracleAsync_NonSuccessStatusCode_ThrowsWithStatusCode()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Server error", Encoding.UTF8, "text/plain")
        };
        var client = CreateClient(response);
        var request = JsonSerializer.SerializeToElement(new { });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.CallOracleAsync(request));
        Assert.Contains("500", ex.Message);
        Assert.Contains("Server error", ex.Message);
    }

    [Fact]
    public async Task CallOracleAsync_EmptyBody_ThrowsInvalidOperation()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("", Encoding.UTF8, "application/json")
        };
        var client = CreateClient(response);
        var request = JsonSerializer.SerializeToElement(new { });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.CallOracleAsync(request));
        Assert.Contains("empty", ex.Message);
    }

    [Fact]
    public async Task CallOracleAsync_InvalidJson_ThrowsInvalidOperation()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json", Encoding.UTF8, "application/json")
        };
        var client = CreateClient(response);
        var request = JsonSerializer.SerializeToElement(new { });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.CallOracleAsync(request));
        Assert.Contains("invalid JSON", ex.Message);
    }

    [Fact]
    public async Task CallOracleAsync_CancelledToken_ThrowsOperationCancelled()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        var client = CreateClient(response);
        var request = JsonSerializer.SerializeToElement(new { });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CallOracleAsync(request, cts.Token));
    }

    [Fact]
    public async Task CallPostgreSqlFunctionAsync_NonObjectParameters_ThrowsArgument()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK));
        var parameters = JsonSerializer.SerializeToElement("not an object");

        await Assert.ThrowsAsync<ArgumentException>(() => client.CallPostgreSqlFunctionAsync("db", "func", parameters));
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;
        private readonly Exception? _exception;

        public TestHttpMessageHandler(HttpResponseMessage response) => _response = response;
        public TestHttpMessageHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_exception is not null) throw _exception;
            return Task.FromResult(_response!);
        }
    }
}
