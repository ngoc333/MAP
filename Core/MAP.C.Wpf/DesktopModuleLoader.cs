using System.IO;
using System.Reflection;
using System.Diagnostics;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Models;
using MAP.C.Contract.Modules;
using MAP.C.UI.Localization;
using Microsoft.Extensions.Logging;

namespace MAP.C.Wpf;

public class DesktopModuleLoader : IModuleLoader
{
    private readonly string _modulesRoot;
    private readonly ILanguageService? _langService;
    private readonly ILogger<DesktopModuleLoader> _logger;
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new();
    private readonly Dictionary<string, Type> _cachedTypes = new();

    public event Action<bool>? OnLoadingChanged;
    public event Action<string>? OnError;

    public DesktopModuleLoader(string modulesRoot, ILanguageService? langService, ILogger<DesktopModuleLoader> logger)
    {
        _modulesRoot = modulesRoot;
        _langService = langService;
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
                _logger.LogInformation("Loading desktop module. Assembly={Assembly} Path={Path} Exists={Exists}", menuItem.Assembly, path, File.Exists(path));
                var assembly = Assembly.LoadFrom(path);
                _loadedAssemblies[menuItem.Assembly!] = assembly;
                LoadModuleLocalization(assembly);
            }

            var type = _loadedAssemblies[menuItem.Assembly!].GetType(menuItem.Component!);
            if (type is not null)
            {
                _cachedTypes[cacheKey] = type;
                _logger.LogInformation("Desktop module loaded. Assembly={Assembly} Component={Component} DurationMs={DurationMs}", menuItem.Assembly, menuItem.Component, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            else
                _logger.LogError("Component was not found. Assembly={Assembly} Component={Component}", menuItem.Assembly, menuItem.Component);

            return Task.FromResult(type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Desktop module load failed. Assembly={Assembly} Component={Component} DurationMs={DurationMs}", menuItem.Assembly, menuItem.Component, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
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
        var loader = new EmbeddedResourceLoader();
        var moduleName = assembly.GetName().Name!;
        loader.LoadModuleResourcesAsync(_langService, assembly, moduleName).GetAwaiter().GetResult();
    }
}
