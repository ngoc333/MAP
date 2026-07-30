using MAP.C.Contract.Models;

namespace MAP.C.Runtime.Menus;

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
}
