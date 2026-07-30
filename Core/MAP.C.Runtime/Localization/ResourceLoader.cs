using System.Reflection;
using System.Text.Json;

namespace MAP.C.Runtime.Localization;

public sealed class ResourceLoader : MAP.C.Contract.Localization.IResourceLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<Dictionary<string, Dictionary<string, object>>> LoadJsonAsync(Assembly assembly, string resourceName)
    {
        var result = new Dictionary<string, Dictionary<string, object>>();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return result;

        var data = await JsonSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, JsonElement>>>(stream, JsonOptions);
        if (data is null) return result;

        foreach (var (section, values) in data)
        {
            var sectionDict = new Dictionary<string, object>();
            foreach (var (k, v) in values)
            {
                sectionDict[k] = v;
            }
            result[section] = sectionDict;
        }
        return result;
    }

    public async Task LoadModuleResourcesAsync(
        MAP.C.Contract.Localization.ILanguageService langService,
        Assembly moduleAssembly,
        string moduleName)
    {
        var prefix = $"{moduleAssembly.GetName().Name}.Localization";
        var vi = await LoadJsonAsync(moduleAssembly, $"{prefix}.vi.json");
        var en = await LoadJsonAsync(moduleAssembly, $"{prefix}.en.json");
        await langService.LoadModuleResourcesAsync(moduleName, vi, en);
    }
}
