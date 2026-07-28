namespace MAP.C.Contract.Shell;

public interface IPageHeaderState
{
    PageHeader? Active { get; }
    event Action? Changed;
    void Set(PageHeader header);
}
