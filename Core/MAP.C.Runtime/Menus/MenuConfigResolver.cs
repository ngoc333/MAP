using System.Diagnostics;
using MAP.C.Contract.Config;
using MAP.C.Contract.Database;
using MAP.C.Contract.Models;
using MAP.C.Runtime.Database;
using Microsoft.Extensions.Logging;

namespace MAP.C.Runtime.Menus;

/// <summary>
/// Shared menu resolution logic used by both WPF and Wasm MenuService.
/// Handles the effective menu source and loads the selected menu configuration.
/// </summary>
public static class MenuConfigResolver
{
    /// <summary>
    /// Resolves the effective menu configuration.
    /// </summary>
    /// <param name="localConfig">The local PageConfig loaded from platform-specific source (file/HTTP).</param>
    /// <param name="configService">App config service for MenuSource override (may be null on Wasm).</param>
    /// <param name="dbClient">Database API client for DB menu loading.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="started">Timestamp from Stopwatch.GetTimestamp() for duration tracking.</param>
    /// <returns>The resolved PageConfig.</returns>
    public static async Task<PageConfig> ResolveAsync(
        PageConfig localConfig,
        IAppConfigService? configService,
        IDbApiClient dbClient,
        ILogger logger,
        long started)
    {
        var source = configService?.Current?.MenuSource ?? localConfig.Source;
        PageConfig config;

        if (string.Equals(source, "local", StringComparison.OrdinalIgnoreCase))
        {
            config = localConfig;
        }
        else if (string.Equals(source, "db", StringComparison.OrdinalIgnoreCase))
        {
            config = await DatabaseMenuLoader.LoadAsync(
                dbClient, localConfig.DbName!, localConfig.DbFunction!);
            logger.LogInformation("Database menu loaded. MenuCount={MenuCount}", config.Menus.Count);
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported menu source: '{source}'. Expected 'local' or 'db'.");
        }

        logger.LogInformation("Menu ready. MenuCount={MenuCount} DurationMs={DurationMs}",
            config.Menus.Count, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        return config;
    }
}
