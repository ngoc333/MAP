using MAP.C.UI.Headers;

namespace MAP.C.Runtime.Tests;

public sealed class PageHeaderStateTests
{
    [Fact]
    public void Clear_WhenActiveHeaderExists_ClearsAndNotifies()
    {
        var state = new PageHeaderState();
        var changes = 0;
        state.Changed += () => changes++;
        state.Set(new PageHeader("A", HeaderKind.Default, "title"));

        state.Clear();

        Assert.Null(state.Active);
        Assert.Equal(2, changes);
    }

}
