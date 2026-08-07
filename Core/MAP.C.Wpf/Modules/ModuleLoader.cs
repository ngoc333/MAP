using System.IO;
using System.Reflection;
using System.Diagnostics;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Models;
using MAP.C.Contract.Modules;
using MAP.C.Runtime.Localization;
using Microsoft.Extensions.Logging;

namespace MAP.C.Wpf.Modules;

public sealed class ModuleLoader : IModuleLoader
{
    private readonly string _modulesRoot;
    private readonly ILanguageService? _langService;
    private readonly IResourceLoader _resourceLoader;
    private readonly ILogger<ModuleLoader> _logger;
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new();
    private readonly Dictionary<string, Type> _cachedTypes = new();

    public event Action<bool>? OnLoadingChanged;

    public ModuleLoader(string modulesRoot, ILanguageService? langService, IResourceLoader resourceLoader, ILogger<ModuleLoader> logger)
    {
        _modulesRoot = modulesRoot;
        _langService = langService;
        _resourceLoader = resourceLoader;
        _logger = logger;
    }

    public async Task<Type> LoadComponentAsync(MenuItem menuItem)
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
                var path = Path.Combine(_modulesRoot, menuItem.Assembly);
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(
                        $"Assembly file not found: {Path.GetFullPath(path)}",
                        path);
                }

                _logger.LogInformation("Loading WPF module. Assembly={Assembly} Path={Path}", menuItem.Assembly, path);
                var assembly = Assembly.LoadFrom(path);

                // Commit to cache only after localization succeeds, so a failed
                // localization keeps the assembly retryable on next load.
                await LoadModuleLocalizationAsync(assembly);
                _loadedAssemblies[menuItem.Assembly] = assembly;
            }

            var type = _loadedAssemblies[menuItem.Assembly].GetType(menuItem.Component);
            if (type is null)
            {
                throw new InvalidOperationException(
                    $"Component '{menuItem.Component}' not found in assembly '{menuItem.Assembly}'.");
            }

            _cachedTypes[cacheKey] = type;
            _logger.LogInformation("WPF module loaded. Assembly={Assembly} Component={Component} DurationMs={DurationMs}",
                menuItem.Assembly, menuItem.Component, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return type;
        }
        catch
        {
            throw; // Re-throw — full exception logging is owned by PageNavigator
        }
        finally
        {
            OnLoadingChanged?.Invoke(false);
        }
    }

    private static string CreateCacheKey(string assemblyName, string componentName)
        => $"{assemblyName}|{componentName}";

    private async Task LoadModuleLocalizationAsync(Assembly assembly)
    {
        if (_langService is null) return;
        var moduleName = assembly.GetName().Name!;
        await _resourceLoader.LoadModuleResourcesAsync(_langService, assembly, moduleName);
    }
}
