using MAP.C.Contract.Models;

namespace MAP.C.Contract.Menus;

public static class MenuTree
{
    public static MenuItem? Find(IEnumerable<MenuItem> items, string id)
    {
        foreach (var item in items)
        {
            if (item.Id == id) return item;
            if (item.Children is not null && Find(item.Children, id) is { } found) return found;
        }

        return null;
    }

    /// <summary>
    /// Finds the first navigable page using depth-first traversal.
    /// Preserves list order.
    /// </summary>
    /// <param name="items">The menu items to search.</param>
    /// <returns>The first navigable page, or null if none found.</returns>
    public static MenuItem? FindFirstPage(IEnumerable<MenuItem> items)
    {
        foreach (var item in items)
        {
            if (item.IsPage)
                return item;

            var child = FindFirstPage(item.Children);

            if (child is not null)
                return child;
        }

        return null;
    }
}
