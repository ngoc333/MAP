using System.Dynamic;

namespace MAP.C.Contract.Pages;

public static class PageParams
{
    public static object Create(Action<dynamic> configure)
    {
        var expando = new ExpandoObject();
        configure(expando);
        return expando;
    }
}
