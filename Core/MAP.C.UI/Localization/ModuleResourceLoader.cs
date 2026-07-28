using System.Reflection;
using MAP.C.Contract.Localization;

namespace MAP.C.UI.Localization;

public static class ModuleResourceLoader
{
    public static async Task LoadModuleAsync(
        IResourceLoader loader,
        ILanguageService langService,
        Assembly moduleAssembly,
        string moduleName)
    {
        await loader.LoadModuleResourcesAsync(langService, moduleAssembly, moduleName);
    }
}
