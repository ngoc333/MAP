using System.Diagnostics;
using MAP.C.Contract.Config;
using MAP.C.Contract.Database;
using MAP.C.Contract.Models;
using MAP.C.Runtime.Database;
using Microsoft.Extensions.Logging;

namespace MAP.C.Runtime.Menus;

/// <summary>
/// Shared menu resolution logic used by both WPF and Wasm MenuService.
/// Handles: effective menu source → optional database load → DB failure fallback → SystemMenus registration.
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
    /// <returns>The resolved PageConfig with SystemMenus ensured.</returns>
    public static async Task<PageConfig> ResolveAsync(
        PageConfig localConfig,
        IAppConfigService? configService,
        IDbApiClient dbClient,
        ILogger logger,
        long started)
    {
        var config = localConfig;
        var source = configService?.Current?.MenuSource ?? config.Source;

        if (string.Equals(source, "db", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                config = await DatabaseMenuLoader.LoadAsync(
                    dbClient, config.DbName!, config.DbFunction!);
                logger.LogInformation("Database menu loaded. MenuCount={MenuCount}", config.Menus.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Database menu load failed; preserving local menu.");
                // Preserve the local menu if the remote source is unavailable or invalid.
            }
        }

        FillMissingTitleKeys(config.Menus, localConfig.Menus);

        SystemMenus.EnsureRegistered(config);
        logger.LogInformation("Menu ready. MenuCount={MenuCount} DurationMs={DurationMs}",
            config.Menus.Count, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        return config;
    }

    /// <summary>
    /// Fills missing TitleKey values in target menus from fallback menus matched by Id.
    /// Does not overwrite existing TitleKey values. Recursively processes children.
    /// </summary>
    internal static void FillMissingTitleKeys(
        IEnumerable<MenuItem> target,
        IEnumerable<MenuItem> fallback)
    {
        foreach (var targetItem in target)
        {
            if (string.IsNullOrEmpty(targetItem.TitleKey))
            {
                var fallbackItem = MenuTree.Find(fallback, targetItem.Id);
                if (fallbackItem is not null && !string.IsNullOrEmpty(fallbackItem.TitleKey))
                {
                    targetItem.TitleKey = fallbackItem.TitleKey;
                }
            }

            if (targetItem.Children is not null)
            {
                FillMissingTitleKeys(targetItem.Children, fallback);
            }
        }
    }
}
