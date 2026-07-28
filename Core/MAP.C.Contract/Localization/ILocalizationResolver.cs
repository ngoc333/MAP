namespace MAP.C.Contract.Localization;

public interface ILocalizationResolver
{
    string? Resolve(string key, string language);
}
