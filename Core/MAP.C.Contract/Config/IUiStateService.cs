namespace MAP.C.Contract.Config;

/// <summary>Controls the shared menu and header visibility.</summary>
public interface IUiStateService
{
    /// <summary>Gets whether the menu is visible.</summary>
    bool ShowMenu { get; }

    /// <summary>Gets whether the header is visible.</summary>
    bool ShowHeader { get; }

    /// <summary>Raised when UI visibility changes.</summary>
    event Action? Changed;

    /// <summary>Toggles menu visibility.</summary>
    void ToggleMenu();

    /// <summary>Toggles header visibility.</summary>
    void ToggleHeader();

    /// <summary>Sets menu visibility.</summary>
    void SetMenu(bool visible);

    /// <summary>Sets header visibility.</summary>
    void SetHeader(bool visible);
}
