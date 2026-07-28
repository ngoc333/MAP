using System.Reflection;

namespace MAP.C.Contract.Localization;

public interface IResourceLoader
{
    Task<Dictionary<string, Dictionary<string, object>>> LoadJsonAsync(Assembly assembly, string resourceName);
    Task LoadModuleResourcesAsync(ILanguageService langService, Assembly moduleAssembly, string moduleName);
}
