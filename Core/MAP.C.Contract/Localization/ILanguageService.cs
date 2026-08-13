namespace MAP.C.Contract.Localization;

/// <summary>Provides localized text for MAP core and module resources.</summary>
public interface ILanguageService
{
    /// <summary>Gets the active language code.</summary>
    string CurrentLanguage { get; }

    /// <summary>Gets available application languages.</summary>
    IReadOnlyList<LanguageInfo> AvailableLanguages { get; }

    /// <summary>Raised after the active language changes.</summary>
    event Action? LanguageChanged;

    /// <summary>Translates a resource key.</summary>
    string T(string key);

    /// <summary>Translates a resource key, returning <paramref name="defaultValue"/> when absent.</summary>
    string T(string key, string defaultValue);

    /// <summary>Sets the active language.</summary>
    void SetLanguage(string language);

    /// <summary>Loads Vietnamese and English resources embedded by a module.</summary>
    Task LoadModuleResourcesAsync(string moduleName, Dictionary<string, Dictionary<string, object>> vi, Dictionary<string, Dictionary<string, object>> en);
}

/// <summary>Describes an available display language.</summary>
public record LanguageInfo(string Code, string Name, string Flag);
