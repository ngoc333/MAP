namespace MAP.C.Contract.Navigation;

/// <summary>Provides navigation between MAP module pages.</summary>
public interface IPageNavigator
{
    /// <summary>Gets the currently active page.</summary>
    ActivePage? Current { get; }

    /// <summary>Gets whether navigation can return to a previous page.</summary>
    bool CanBack { get; }

    /// <summary>Raised when the active page changes.</summary>
    event Action? Changed;

    /// <summary>Opens a page, optionally passing an anonymous object or <see cref="PageParams"/>.</summary>
    Task OpenAsync(string pageId, object? parameters = null);

    /// <summary>Returns to the previous page.</summary>
    Task BackAsync();
}
