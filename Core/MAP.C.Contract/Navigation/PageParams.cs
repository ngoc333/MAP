using System.Dynamic;

namespace MAP.C.Contract.Navigation;

/// <summary>
/// A contract-level parameter bag passed between independently loaded pages.
/// </summary>
public sealed class PageParams : DynamicObject
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a parameter bag from an existing bag or the readable public properties of an object.
    /// </summary>
    /// <param name="parameters">A parameter bag, anonymous object, or <see langword="null"/>.</param>
    /// <param name="exception">The reflection error when conversion fails; otherwise <see langword="null"/>.</param>
    /// <returns>A parameter bag, or <see langword="null"/> when <paramref name="parameters"/> is <see langword="null"/> or conversion fails.</returns>
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

    /// <summary>Gets or sets a parameter by name.</summary>
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
