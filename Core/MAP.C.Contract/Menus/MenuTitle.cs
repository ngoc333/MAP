using MAP.C.Contract.Models;

namespace MAP.C.Contract.Menus;

/// <summary>
/// Resolves the display title for a menu item based on the current language.
/// Resolution order: current language -> default language -> first available non-empty title -> menu id.
/// </summary>
public static class MenuTitle
{
    /// <summary>
    /// Gets the localized title for a menu item.
    /// </summary>
    /// <param name="item">The menu item.</param>
    /// <param name="language">The current language code (e.g., "vi", "en").</param>
    /// <param name="defaultLanguage">The default fallback language. Defaults to "vi".</param>
    /// <returns>The resolved title string.</returns>
    public static string Get(
        MenuItem item,
        string language,
        string defaultLanguage = "vi")
    {
        // Try current language first
        if (item.Titles.TryGetValue(language, out var title) &&
            !string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        // Try default language
        if (item.Titles.TryGetValue(defaultLanguage, out title) &&
            !string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        // Try first available non-empty title
        title = item.Titles.Values
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return title ?? item.Id;
    }
}
