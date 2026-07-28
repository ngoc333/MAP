using System.Dynamic;

namespace MAP.C.Contract.UI.Pages;

public static class PageParams
{
    public static object Create(Action<dynamic> configure)
    {
        var expando = new ExpandoObject();
        configure(expando);
        return expando;
    }
}
