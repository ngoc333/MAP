using MAP.C.Contract.Models;

namespace MAP.C.Contract.Menus;

/// <summary>Loads and exposes the host menu tree used to open module pages.</summary>
public interface IMenuService
{
    /// <summary>Gets the loaded root menu items.</summary>
    List<MenuItem> Menus { get; }

    /// <summary>Gets the configured initial page identifier, when available.</summary>
    string? StartPageId => null;

    /// <summary>Gets the configured database name for the active menu configuration, when available.</summary>
    string? DbName => null;

    /// <summary>Gets whether the current host can edit its local page.json file.</summary>
    bool SupportsLocalConfigEditing => false;

    /// <summary>Gets the local page.json path or location shown to the user.</summary>
    string? LocalConfigLocation => null;

    /// <summary>Raised after the menu configuration has been loaded.</summary>
    event Action? OnMenusLoaded;

    /// <summary>Loads the menu configuration from the configured source.</summary>
    Task LoadMenusAsync();

    /// <summary>Reads the editable local page.json content when supported by the host.</summary>
    Task<string?> ReadLocalConfigAsync() => Task.FromResult<string?>(null);

    /// <summary>Saves the editable local page.json content when supported by the host.</summary>
    Task SaveLocalConfigAsync(string json) => Task.FromException(
        new NotSupportedException("Local page.json editing is not supported by this host."));

    /// <summary>Finds a menu item by its identifier.</summary>
    /// <param name="id">The menu or page identifier to find.</param>
    /// <returns>The matching menu item, or <see langword="null"/> when no item exists.</returns>
    MenuItem? FindById(string id);
}
