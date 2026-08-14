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

    /// <summary>
    /// Opens a page, optionally passing an anonymous object or <see cref="PageParams"/>.
    /// When <paramref name="pushHistory"/> is <see langword="true"/>, the current page is saved to the back stack.
    /// When it is <see langword="false"/>, the back stack remains unchanged.
    /// </summary>
    Task OpenAsync(string pageId, object? parameters = null, bool pushHistory = true);

    /// <summary>
    /// Opens a root page after successfully resolving it, then clears the complete back-navigation history.
    /// </summary>
    Task OpenRootAsync(string pageId, object? parameters = null);

    /// <summary>Returns to the previous page.</summary>
    Task BackAsync();
}
