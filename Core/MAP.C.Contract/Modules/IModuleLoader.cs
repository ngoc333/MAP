using MAP.C.Contract.Models;

namespace MAP.C.Contract.Modules;

public interface IModuleLoader
{
    event Action<bool>? OnLoadingChanged;
    Task<Type> LoadComponentAsync(MenuItem menuItem);
}
