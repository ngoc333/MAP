namespace MAP.C.Contract.Localization;

public interface ILanguageService
{
    string CurrentLanguage { get; }
    IReadOnlyList<LanguageInfo> AvailableLanguages { get; }
    event Action? LanguageChanged;

    string T(string key);
    string T(string key, string defaultValue);
    void SetLanguage(string language);
    Task LoadModuleResourcesAsync(string moduleName, Dictionary<string, Dictionary<string, object>> vi, Dictionary<string, Dictionary<string, object>> en);
}

public record LanguageInfo(string Code, string Name, string Flag);
