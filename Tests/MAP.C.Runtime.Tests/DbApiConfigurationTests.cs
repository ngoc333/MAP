using System.Text;
using System.Text.Json;
using MAP.C.Runtime.Database;

namespace MAP.C.Runtime.Tests;

public sealed class DbApiConfigurationTests
{
    [Fact]
    public void Load_MissingProperty_ThrowsInvalidOperation()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
            { "OracleApiBaseUrl": "https://oracle.example/" }
            """));

        Assert.Throws<InvalidOperationException>(() => DbApiConfiguration.Load(stream));
    }

    [Fact]
    public void Load_RelativeUrl_ThrowsInvalidOperation()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
            { "OracleApiBaseUrl": "not-a-url", "PostgreSqlApiBaseUrl": "https://postgres.example/" }
            """));

        Assert.Throws<InvalidOperationException>(() => DbApiConfiguration.Load(stream));
    }

    [Fact]
    public void Load_InvalidJson_ThrowsJsonException()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{ invalid json }"));

        Assert.ThrowsAny<JsonException>(() => DbApiConfiguration.Load(stream));
    }

    [Fact]
    public void Load_EmptyObject_ThrowsInvalidOperation()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        Assert.Throws<InvalidOperationException>(() => DbApiConfiguration.Load(stream));
    }

    [Fact]
    public void Load_ValidConfig_ParsesCorrectly()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
            { "OracleApiBaseUrl": "https://oracle.example/", "PostgreSqlApiBaseUrl": "https://postgres.example/" }
            """));

        var config = DbApiConfiguration.Load(stream);

        Assert.Equal(new Uri("https://oracle.example/"), config.OracleBaseAddress);
        Assert.Equal(new Uri("https://postgres.example/"), config.PostgreSqlBaseAddress);
    }
}
