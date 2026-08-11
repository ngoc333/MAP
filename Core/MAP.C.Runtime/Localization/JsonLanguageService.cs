using System.Globalization;
using System.Reflection;
using MAP.C.Contract.Localization;

namespace MAP.C.Runtime.Localization;

public sealed class JsonLanguageService : ILanguageService
{
    private readonly Dictionary<string, Dictionary<string, object>> _vi = new();
    private readonly Dictionary<string, Dictionary<string, object>> _en = new();
    private readonly IResourceLoader _loader;
    private string _currentLanguage = "vi";

    public JsonLanguageService(IResourceLoader loader)
    {
        _loader = loader;
    }

    public string CurrentLanguage => _currentLanguage;

    public IReadOnlyList<LanguageInfo> AvailableLanguages { get; } = new[]
    {
        new LanguageInfo("vi", "Tiếng Việt", "vi"),
        new LanguageInfo("en", "English", "en")
    };

    public event Action? LanguageChanged;

    public async Task InitializeAsync(Assembly sharedAssembly)
    {
        var prefix = $"{sharedAssembly.GetName().Name}.Localization";
        var viData = await _loader.LoadJsonAsync(sharedAssembly, $"{prefix}.vi.json");
        var enData = await _loader.LoadJsonAsync(sharedAssembly, $"{prefix}.en.json");
        Merge(_vi, viData);
        Merge(_en, enData);
    }

    public Task LoadModuleResourcesAsync(
        string moduleName,
        Dictionary<string, Dictionary<string, object>> vi,
        Dictionary<string, Dictionary<string, object>> en)
    {
        Merge(_vi, vi);
        Merge(_en, en);
        return Task.CompletedTask;
    }

    public string T(string key)
        => T(key, key);

    public string T(string key, string defaultValue)
    {
        var dict = _currentLanguage == "vi" ? _vi : _en;
        var result = ResolveNested(key, dict);
        if (result is not null) return result;

        var fallback = _currentLanguage == "vi" ? _en : _vi;
        result = ResolveNested(key, fallback);
        if (result is not null) return result;

        return defaultValue;
    }

    public void SetLanguage(string language)
    {
        if (!AvailableLanguages.Any(l => l.Code == language))
        {
            throw new ArgumentException(
                $"Language '{language}' is not available. Available: {string.Join(", ", AvailableLanguages.Select(l => l.Code))}",
                nameof(language));
        }

        var changed = _currentLanguage != language;
        _currentLanguage = language;

        var culture = new CultureInfo(language == "vi" ? "vi-VN" : "en-US");
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;

        if (changed)
            LanguageChanged?.Invoke();
    }

    private static string? ResolveNested(string key, Dictionary<string, Dictionary<string, object>> dict)
    {
        var parts = key.Split('.', 2);
        if (parts.Length < 2) return null;

        var section = parts[0];
        var nestedKey = parts[1];

        if (!dict.TryGetValue(section, out var sectionDict))
            return null;

        return ResolveFromDict(nestedKey, sectionDict);
    }

    private static string? ResolveFromDict(string key, Dictionary<string, object> dict)
    {
        if (dict.TryGetValue(key, out var value))
        {
            if (value is System.Text.Json.JsonElement je)
                return je.ValueKind == System.Text.Json.JsonValueKind.String ? je.GetString() : je.ToString();
            return value?.ToString();
        }
        return null;
    }

    private static void Merge(
        Dictionary<string, Dictionary<string, object>> target,
        Dictionary<string, Dictionary<string, object>> source)
    {
        foreach (var (section, values) in source)
        {
            if (!target.TryGetValue(section, out var existing))
            {
                existing = new Dictionary<string, object>();
                target[section] = existing;
            }
            foreach (var (k, v) in values)
            {
                existing[k] = v;
            }
        }
    }
}
