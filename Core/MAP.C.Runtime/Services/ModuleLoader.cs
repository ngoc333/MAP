using System.Reflection;
using MAP.C.Contract.Models;
using MAP.C.Contract.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Services;

namespace MAP.C.Runtime.Services;

public class ModuleLoader : IModuleLoader
{
    private readonly LazyAssemblyLoader _assemblyLoader;
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new();
    private readonly Dictionary<string, Type> _cachedTypes = new();

    public event Action<bool>? OnLoadingChanged;
    public event Action<string>? OnError;

    public ModuleLoader(LazyAssemblyLoader assemblyLoader)
    {
        _assemblyLoader = assemblyLoader;
    }

    public async Task<Type?> LoadComponentAsync(MenuItem menuItem)
    {
        if (string.IsNullOrEmpty(menuItem.Assembly) || string.IsNullOrEmpty(menuItem.Component))
            return null;

        var cacheKey = menuItem.Component;

        if (_cachedTypes.TryGetValue(cacheKey, out var cachedType))
            return cachedType;

        try
        {
            OnLoadingChanged?.Invoke(true);

            if (!_loadedAssemblies.ContainsKey(menuItem.Assembly))
            {
                var assemblies = (await _assemblyLoader.LoadAssembliesAsync(new[] { menuItem.Assembly })).ToList();
                if (assemblies.Count > 0)
                    _loadedAssemblies[menuItem.Assembly] = assemblies[0];
            }

            var assembly = _loadedAssemblies[menuItem.Assembly];
            var type = assembly.GetType(menuItem.Component);

            if (type is not null)
            {
                _cachedTypes[cacheKey] = type;
                return type;
            }

            OnError?.Invoke($"Không tìm thấy component '{menuItem.Component}' trong assembly '{menuItem.Assembly}'");
            return null;
        }
        catch (Exception ex)
        {
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
}
