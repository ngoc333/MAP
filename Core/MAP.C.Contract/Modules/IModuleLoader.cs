using MAP.C.Contract.Models;

namespace MAP.C.Contract.Modules;

public interface IModuleLoader
{
    event Action<bool>? OnLoadingChanged;
    event Action<string>? OnError;
    Task<Type?> LoadComponentAsync(MenuItem menuItem);
    Type? GetCachedType(string assemblyName, string componentName);
}
