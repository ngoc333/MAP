using MAP.C.Contract.Models;

namespace MAP.C.Contract.Menus;

/// <summary>
/// Validates MAP menu configuration. A node is either a group with children or
/// a navigable page with an assembly and component, never both.
/// </summary>
public static class MenuConfigValidator
{
    public static void Validate(PageConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        foreach (var item in config.Menus)
            ValidateItem(item);
    }

    private static void ValidateItem(MenuItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
            throw new InvalidOperationException("Menu item id must not be empty.");

        if (!item.Titles.Values.Any(title => !string.IsNullOrWhiteSpace(title)))
        {
            throw new InvalidOperationException(
                $"Menu item '{item.Id}' does not contain any localized title.");
        }

        var hasAssembly = !string.IsNullOrWhiteSpace(item.Assembly);
        var hasComponent = !string.IsNullOrWhiteSpace(item.Component);
        if (hasAssembly != hasComponent)
        {
            throw new InvalidOperationException(
                $"Menu page '{item.Id}' must contain both Assembly and Component.");
        }

        if (item.IsPage && item.HasChildren)
        {
            throw new InvalidOperationException(
                $"Menu item '{item.Id}' cannot be both a page and a group.");
        }

        foreach (var child in item.Children)
            ValidateItem(child);
    }
}
