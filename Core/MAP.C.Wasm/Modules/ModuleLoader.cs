using System.Reflection;
using System.Diagnostics;
using MAP.C.Contract.Models;
using MAP.C.Contract.Modules;
using Microsoft.AspNetCore.Components.WebAssembly.Services;
using Microsoft.Extensions.Logging;
using MAP.C.Contract.Localization;

namespace MAP.C.Wasm.Modules;

public class ModuleLoader : IModuleLoader
{
    private readonly LazyAssemblyLoader _assemblyLoader;
    private readonly ILanguageService _langService;
    private readonly IResourceLoader _resourceLoader;
    private readonly ILogger<ModuleLoader> _logger;
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new();
    private readonly Dictionary<string, Type> _cachedTypes = new();

    public event Action<bool>? OnLoadingChanged;
    public event Action<string>? OnError;

    public ModuleLoader(LazyAssemblyLoader assemblyLoader, ILanguageService langService, IResourceLoader resourceLoader, ILogger<ModuleLoader> logger)
    {
        _assemblyLoader = assemblyLoader;
        _langService = langService;
        _resourceLoader = resourceLoader;
        _logger = logger;
    }

    public async Task<Type?> LoadComponentAsync(MenuItem menuItem)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(menuItem.Assembly))
            throw new ArgumentException("MenuItem.Assembly is null or empty.", nameof(menuItem));
        if (string.IsNullOrWhiteSpace(menuItem.Component))
            throw new ArgumentException("MenuItem.Component is null or empty.", nameof(menuItem));

        var started = Stopwatch.GetTimestamp();
        var cacheKey = CreateCacheKey(menuItem.Assembly, menuItem.Component);

        if (_cachedTypes.TryGetValue(cacheKey, out var cachedType))
        {
            _logger.LogInformation("Module cache hit. Assembly={Assembly} Component={Component}", menuItem.Assembly, menuItem.Component);
            return cachedType;
        }

        try
        {
            OnLoadingChanged?.Invoke(true);

            if (!_loadedAssemblies.ContainsKey(menuItem.Assembly))
            {
                _logger.LogInformation("Lazy loading web module. Assembly={Assembly}", menuItem.Assembly);
                var assemblies = (await _assemblyLoader
                    .LoadAssembliesAsync(new[] { menuItem.Assembly }))
                    .ToList();

                if (assemblies.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"LoadAssembliesAsync returned empty list for assembly '{menuItem.Assembly}'.");
                }

                var loadedAssembly = assemblies[0];

                // Commit to cache only after localization succeeds, so a failed
                // localization keeps the assembly retryable on next load.
                await LoadModuleLocalizationAsync(loadedAssembly);
                _loadedAssemblies[menuItem.Assembly] = loadedAssembly;
            }

            var assembly = _loadedAssemblies[menuItem.Assembly];
            var type = assembly.GetType(menuItem.Component);

            if (type is null)
            {
                throw new InvalidOperationException(
                    $"Component '{menuItem.Component}' not found in assembly '{menuItem.Assembly}'.");
            }

            _cachedTypes[cacheKey] = type;
            _logger.LogInformation("Web module loaded. Assembly={Assembly} Component={Component} DurationMs={DurationMs}",
                menuItem.Assembly, menuItem.Component, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return type;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Web module load failed. Assembly={Assembly} Component={Component} DurationMs={DurationMs}",
                menuItem.Assembly, menuItem.Component, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            OnError?.Invoke($"Lỗi tải module '{menuItem.Assembly}': {ex.Message}");
            throw; // Re-throw to preserve original exception
        }
        finally
        {
            OnLoadingChanged?.Invoke(false);
        }
    }

    public Type? GetCachedType(string assemblyName, string componentName)
    {
        var cacheKey = CreateCacheKey(assemblyName, componentName);
        _cachedTypes.TryGetValue(cacheKey, out var type);
        return type;
    }

    private static string CreateCacheKey(string assemblyName, string componentName)
        => $"{assemblyName}|{componentName}";

    private async Task LoadModuleLocalizationAsync(Assembly assembly)
    {
        var moduleName = assembly.GetName().Name!;
        await _resourceLoader.LoadModuleResourcesAsync(_langService, assembly, moduleName);
    }
}
