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

        if (config.Menus is null)
        {
            throw new InvalidOperationException(
                "Menu configuration does not contain a menu collection.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in config.Menus)
            ValidateItem(item, ids);
    }

    private static void ValidateItem(MenuItem item, HashSet<string> ids)
    {
        if (item is null)
        {
            throw new InvalidOperationException(
                "Menu configuration contains a null menu item.");
        }

        if (string.IsNullOrWhiteSpace(item.Id))
        {
            throw new InvalidOperationException(
                "Menu item id must not be empty.");
        }

        if (!ids.Add(item.Id))
        {
            throw new InvalidOperationException(
                $"Duplicate menu item id '{item.Id}'.");
        }

        if (item.Titles is null)
        {
            throw new InvalidOperationException(
                $"Menu item '{item.Id}' does not contain a titles collection.");
        }

        if (!item.Titles.Values.Any(title => !string.IsNullOrWhiteSpace(title)))
        {
            throw new InvalidOperationException(
                $"Menu item '{item.Id}' does not contain any localized title.");
        }

        if (item.Children is null)
        {
            throw new InvalidOperationException(
                $"Menu item '{item.Id}' does not contain a children collection.");
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

        if (!item.IsPage && !item.HasChildren)
        {
            throw new InvalidOperationException(
                $"Menu item '{item.Id}' must be either a page or a group.");
        }

        foreach (var child in item.Children)
            ValidateItem(child, ids);
    }
}
