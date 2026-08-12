using MAP.C.Contract.Models;

namespace MAP.C.Contract.Menus;

public interface IMenuService
{
    List<MenuItem> Menus { get; }
    string? StartPageId => null;
    string? DbName => null;
    event Action? OnMenusLoaded;
    Task LoadMenusAsync();
    MenuItem? FindById(string id);
}
