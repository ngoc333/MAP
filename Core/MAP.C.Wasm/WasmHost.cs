using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MAP.C.Contract.Config;
using MAP.C.Contract.Database;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Logging;
using MAP.C.Runtime.Database;
using MAP.C.Runtime.Localization;
using MAP.C.Wasm.Config;
using MAP.C.UI.Layout;

namespace MAP.C.Wasm;

public static class WasmHost
{
    public static async Task RunAsync(string[] args)
    {
        var started = Stopwatch.GetTimestamp();
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        // Register MainLayout as root component (no Router needed)
        builder.RootComponents.Add<MainLayout>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };

        // Register Wasm platform services
        builder.Services.AddWasm(http);

        // Register database API configuration with fallback
        await RegisterDbApiAsync(builder, http);

        var host = builder.Build();

        // Initialize language service
        var langService = host.Services.GetRequiredService<ILanguageService>();
        if (langService is JsonLanguageService jsonLang)
            await jsonLang.InitializeAsync(typeof(JsonLanguageService).Assembly);

        // Log startup
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AppStartup");
        logger.LogInformation(
            "Web application starting. SessionId={SessionId} BaseAddress={BaseAddress} DurationMs={DurationMs}",
            DiagnosticContext.SessionId, builder.HostEnvironment.BaseAddress,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        // Load configuration
        await host.Services.GetRequiredService<AppConfigService>().LoadAsync();

        await host.RunAsync();
    }

    private static async Task RegisterDbApiAsync(WebAssemblyHostBuilder builder, HttpClient http)
    {
        try
        {
            await using var dbApiConfigurationStream = await http.GetStreamAsync("db-api.json");
            var dbApiConfiguration = await DbApiConfiguration.LoadAsync(dbApiConfigurationStream);
            builder.Services.AddWasmDbApi(dbApiConfiguration);
        }
        catch (Exception ex)
        {
            // Register fallback DB client that fails gracefully
            builder.Services.AddSingleton<IDbApiClient>(sp =>
            {
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbApiFallback");
                logger.LogError(ex, "Failed to load db-api.json. Using fallback DB client.");
                return new FallbackDbApiClient(logger);
            });
        }
    }
}

/// <summary>
/// Fallback DB client that returns controlled errors when db-api.json is unavailable.
/// </summary>
internal sealed class FallbackDbApiClient : IDbApiClient
{
    private readonly ILogger _logger;

    public FallbackDbApiClient(ILogger logger)
    {
        _logger = logger;
    }

    public Task<JsonElement> CallOracleAsync(JsonElement requestBody, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("DB operation attempted but db-api.json is not configured. Operation=CallOracleAsync");
        return Task.FromResult(JsonDocument.Parse("{}").RootElement);
    }

    public Task<JsonElement> CallPostgreSqlFunctionAsync(string dbName, string commandName, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("DB operation attempted but db-api.json is not configured. Operation=CallPostgreSqlFunctionAsync DbName={DbName} CommandName={CommandName}", dbName, commandName);
        return Task.FromResult(JsonDocument.Parse("{}").RootElement);
    }

    public Task<JsonElement> CallPostgreSqlProcedureAsync(string dbName, string commandName, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("DB operation attempted but db-api.json is not configured. Operation=CallPostgreSqlProcedureAsync DbName={DbName} CommandName={CommandName}", dbName, commandName);
        return Task.FromResult(JsonDocument.Parse("{}").RootElement);
    }
}