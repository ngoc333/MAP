using MAP.C.Contract.Models;

namespace MAP.C.Contract.Services;

public interface IModuleLoader
{
    event Action<bool>? OnLoadingChanged;
    event Action<string>? OnError;
    Task<Type?> LoadComponentAsync(MenuItem menuItem);
    Type? GetCachedType(string componentName);
}
