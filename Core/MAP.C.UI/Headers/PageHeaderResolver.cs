using MAP.C.Contract.Models;
using MAP.C.Contract.Navigation;

namespace MAP.C.UI.Headers;

/// <summary>
/// Resolves the header belonging to the current page without retaining UI state.
/// </summary>
public static class PageHeaderResolver
{
    public static PageHeader? GetMatchingHeader(
        ActivePage? current,
        PageHeader? header)
    {
        if (current is null || header is null)
            return null;

        return header.PageId == current.PageId ? header : null;
    }

    public static string ResolveTitle(
        ActivePage? current,
        PageHeader? header,
        Func<MenuItem, string> resolveMenuTitle,
        Func<string, string, string> translate)
    {
        ArgumentNullException.ThrowIfNull(resolveMenuTitle);
        ArgumentNullException.ThrowIfNull(translate);

        if (current is null)
            throw new ArgumentNullException(nameof(current));

        var menuTitle = resolveMenuTitle(current.MenuItem);
        var matchingHeader = GetMatchingHeader(current, header);

        return matchingHeader?.TitleKey is { Length: > 0 } titleKey
            ? translate(titleKey, menuTitle)
            : menuTitle;
    }
}
