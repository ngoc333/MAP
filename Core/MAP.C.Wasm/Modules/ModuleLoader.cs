using System.Reflection;
using System.Diagnostics;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Models;
using MAP.C.Contract.Modules;
using MAP.C.UI.Localization;
using Microsoft.AspNetCore.Components.WebAssembly.Services;
using Microsoft.Extensions.Logging;

namespace MAP.C.Wasm.Modules;

public class ModuleLoader : IModuleLoader
{
    private readonly LazyAssemblyLoader _assemblyLoader;
    private readonly ILanguageService _langService;
    private readonly ILogger<ModuleLoader> _logger;
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new();
    private readonly Dictionary<string, Type> _cachedTypes = new();

    public event Action<bool>? OnLoadingChanged;
    public event Action<string>? OnError;

    public ModuleLoader(LazyAssemblyLoader assemblyLoader, ILanguageService langService, ILogger<ModuleLoader> logger)
    {
        _assemblyLoader = assemblyLoader;
        _langService = langService;
        _logger = logger;
    }

    public async Task<Type?> LoadComponentAsync(MenuItem menuItem)
    {
        var started = Stopwatch.GetTimestamp();
        if (string.IsNullOrEmpty(menuItem.Assembly) || string.IsNullOrEmpty(menuItem.Component))
            return null;

        var cacheKey = menuItem.Component;

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
                var assemblies = (await _assemblyLoader.LoadAssembliesAsync(new[] { menuItem.Assembly })).ToList();
                if (assemblies.Count > 0)
                {
                    _loadedAssemblies[menuItem.Assembly] = assemblies[0];
                    await LoadModuleLocalizationAsync(assemblies[0]);
                }
            }

            var assembly = _loadedAssemblies[menuItem.Assembly];
            var type = assembly.GetType(menuItem.Component);

            if (type is not null)
            {
                _cachedTypes[cacheKey] = type;
                _logger.LogInformation("Web module loaded. Assembly={Assembly} Component={Component} DurationMs={DurationMs}", menuItem.Assembly, menuItem.Component, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                return type;
            }

            OnError?.Invoke($"Không tìm thấy component '{menuItem.Component}' trong assembly '{menuItem.Assembly}'");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Web module load failed. Assembly={Assembly} Component={Component} DurationMs={DurationMs}", menuItem.Assembly, menuItem.Component, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            OnError?.Invoke($"Lỗi tải module '{menuItem.Assembly}': {ex.Message}");
            return null;
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

    private async Task LoadModuleLocalizationAsync(Assembly assembly)
    {
        var loader = new EmbeddedResourceLoader();
        var moduleName = assembly.GetName().Name!;
        await loader.LoadModuleResourcesAsync(_langService, assembly, moduleName);
    }
}
