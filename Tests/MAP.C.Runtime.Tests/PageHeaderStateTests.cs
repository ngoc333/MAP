using MAP.C.UI.Headers;

namespace MAP.C.Runtime.Tests;

public sealed class PageHeaderStateTests
{
    [Fact]
    public void Clear_WhenActiveHeaderBelongsToPage_ClearsAndNotifies()
    {
        var state = new PageHeaderState();
        var changes = 0;
        state.Changed += () => changes++;
        state.Set(new PageHeader("A", HeaderKind.Default, "title"));

        state.Clear("A");

        Assert.Null(state.Active);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Clear_WhenActiveHeaderBelongsToAnotherPage_DoesNothing()
    {
        var state = new PageHeaderState();
        var changes = 0;
        state.Changed += () => changes++;
        var header = new PageHeader("B", HeaderKind.Default, "title");
        state.Set(header);

        state.Clear("A");

        Assert.Same(header, state.Active);
        Assert.Equal(1, changes);
    }
}
