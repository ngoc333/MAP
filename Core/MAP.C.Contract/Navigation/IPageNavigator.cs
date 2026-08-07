namespace MAP.C.Contract.Navigation;

public interface IPageNavigator
{
    ActivePage? Current { get; }
    bool CanBack { get; }
    event Action? Changed;
    Task OpenAsync(string pageId, object? parameters = null);
    Task OpenAsync(string pageId, bool forceReopen, object? parameters = null);
    Task BackAsync();
}
