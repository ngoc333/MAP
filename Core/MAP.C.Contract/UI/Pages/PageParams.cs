using System.Dynamic;

namespace MAP.C.Contract.UI.Pages;

/// <summary>
/// A contract-level parameter bag passed between independently loaded pages.
/// </summary>
public sealed class PageParams : DynamicObject
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    public static PageParams? From(object? parameters, out Exception? exception)
    {
        exception = null;

        if (parameters is null)
            return null;

        if (parameters is PageParams pageParams)
            return pageParams;

        try
        {
            var result = new PageParams();
            foreach (var property in parameters.GetType().GetProperties())
                if (property.CanRead && property.GetIndexParameters().Length == 0)
                    result[property.Name] = property.GetValue(parameters);

            return result;
        }
        catch (Exception ex)
        {
            exception = ex;
            return null;
        }
    }

    public object? this[string name]
    {
        get => _values.TryGetValue(name, out var value) ? value : null;
        set => _values[name] = value;
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result) =>
        _values.TryGetValue(binder.Name, out result);

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        _values[binder.Name] = value;
        return true;
    }
}
