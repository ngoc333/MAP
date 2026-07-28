using System.Globalization;
using MAP.C.Contract.Localization;

namespace MAP.C.UI.Localization;

public sealed class RadzenLocalizer : Radzen.ILocalizer
{
    private readonly ILanguageService _lang;

    public RadzenLocalizer(ILanguageService lang) => _lang = lang;

    public string? Get(string key, CultureInfo culture)
    {
        var result = _lang.T($"radzen.{key}");
        return result == $"radzen.{key}" ? null : result;
    }
}
