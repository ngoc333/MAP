using System.IO;
using System.Reflection;
using MAP.C.Contract.Models;
using MAP.C.Contract.Services;

namespace MAP.C.Wpf;

public class DesktopModuleLoader : IModuleLoader
{
    private readonly string _modulesRoot;
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new();
    private readonly Dictionary<string, Type> _cachedTypes = new();

    public event Action<bool>? OnLoadingChanged;
    public event Action<string>? OnError;

    public DesktopModuleLoader(string modulesRoot)
    {
        _modulesRoot = modulesRoot;
    }

    public Task<Type?> LoadComponentAsync(MenuItem menuItem)
    {
        var cacheKey = menuItem.Component!;
        if (_cachedTypes.TryGetValue(cacheKey, out var cachedType))
            return Task.FromResult<Type?>(cachedType);

        try
        {
            OnLoadingChanged?.Invoke(true);

            if (!_loadedAssemblies.ContainsKey(menuItem.Assembly!))
            {
                var path = Path.Combine(_modulesRoot, menuItem.Assembly!);
                _loadedAssemblies[menuItem.Assembly!] = Assembly.LoadFrom(path);
            }

            var type = _loadedAssemblies[menuItem.Assembly!].GetType(menuItem.Component!);
            if (type is not null)
                _cachedTypes[cacheKey] = type;

            return Task.FromResult(type);
        }
        catch (Exception ex)
        {
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
}
