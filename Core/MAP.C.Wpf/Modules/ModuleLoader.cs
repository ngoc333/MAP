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
    public event Action<string>? OnError;

    public ModuleLoader(string modulesRoot, ILanguageService? langService, IResourceLoader resourceLoader, ILogger<ModuleLoader> logger)
    {
        _modulesRoot = modulesRoot;
        _langService = langService;
        _resourceLoader = resourceLoader;
        _logger = logger;
    }

    public Task<Type?> LoadComponentAsync(MenuItem menuItem)
    {
        var started = Stopwatch.GetTimestamp();
        var cacheKey = menuItem.Component!;
        if (_cachedTypes.TryGetValue(cacheKey, out var cachedType))
        {
            _logger.LogInformation("Module cache hit. Assembly={Assembly} Component={Component}", menuItem.Assembly, menuItem.Component);
            return Task.FromResult<Type?>(cachedType);
        }

        try
        {
            OnLoadingChanged?.Invoke(true);

            if (!_loadedAssemblies.ContainsKey(menuItem.Assembly!))
            {
                var path = Path.Combine(_modulesRoot, menuItem.Assembly!);
                _logger.LogInformation("Loading WPF module. Assembly={Assembly} Path={Path} Exists={Exists}", menuItem.Assembly, path, File.Exists(path));
                var assembly = Assembly.LoadFrom(path);
                _loadedAssemblies[menuItem.Assembly!] = assembly;
                LoadModuleLocalization(assembly);
            }

            var type = _loadedAssemblies[menuItem.Assembly!].GetType(menuItem.Component!);
            if (type is not null)
            {
                _cachedTypes[cacheKey] = type;
                _logger.LogInformation("WPF module loaded. Assembly={Assembly} Component={Component} DurationMs={DurationMs}", menuItem.Assembly, menuItem.Component, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            else
                _logger.LogError("Component was not found. Assembly={Assembly} Component={Component}", menuItem.Assembly, menuItem.Component);

            return Task.FromResult(type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WPF module load failed. Assembly={Assembly} Component={Component} DurationMs={DurationMs}", menuItem.Assembly, menuItem.Component, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            OnError?.Invoke($"Failed to load module '{menuItem.Assembly}': {ex.Message}");
            return Task.FromResult<Type?>(null);
        }
        finally
        {
            OnLoadingChanged?.Invoke(false);
        }
    }

    public Type? GetCachedType(string componentName)
    {
        _cachedTypes.TryGetValue(componentName, out var type);
        return type;
    }

    private void LoadModuleLocalization(Assembly assembly)
    {
        if (_langService is null) return;
        var moduleName = assembly.GetName().Name!;
        _resourceLoader.LoadModuleResourcesAsync(_langService, assembly, moduleName).GetAwaiter().GetResult();
    }
}
