using System.Globalization;
using MAP.C.Runtime.Localization;

namespace MAP.C.Runtime.Tests;

public sealed class JsonLanguageServiceTests
{
    private static JsonLanguageService CreateService()
    {
        var loader = new ResourceLoader();
        return new JsonLanguageService(loader);
    }

    [Fact]
    public void T_KeyNotFound_ReturnsDefault()
    {
        var service = CreateService();

        var result = service.T("nonexistent.key", "fallback");

        Assert.Equal("fallback", result);
    }

    [Fact]
    public void T_KeyNotFoundNoDefault_ReturnsKey()
    {
        var service = CreateService();

        var result = service.T("nonexistent.key");

        Assert.Equal("nonexistent.key", result);
    }

    [Fact]
    public void SetLanguage_InvalidCode_ThrowsArgumentException()
    {
        var service = CreateService();

        var ex = Assert.Throws<ArgumentException>(() => service.SetLanguage("fr"));
        Assert.Contains("fr", ex.Message);
        Assert.Contains("available", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetLanguage_ValidCode_UpdatesCurrentLanguage()
    {
        var service = CreateService();

        service.SetLanguage("en");

        Assert.Equal("en", service.CurrentLanguage);
    }

    [Fact]
    public void SetLanguage_SameInitialLanguage_AppliesVietnameseCultureWithoutRaisingEvent()
    {
        var service = CreateService();
        var eventCount = 0;
        service.LanguageChanged += () => eventCount++;

        service.SetLanguage("vi");

        Assert.Equal("vi", service.CurrentLanguage);
        Assert.Equal("vi-VN", CultureInfo.CurrentCulture.Name);
        Assert.Equal("vi-VN", CultureInfo.CurrentUICulture.Name);
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void SetLanguage_ChangesToEnglish_AppliesCultureAndRaisesEventOnce()
    {
        var service = CreateService();
        var eventCount = 0;
        service.LanguageChanged += () => eventCount++;

        service.SetLanguage("en");

        Assert.Equal("en", service.CurrentLanguage);
        Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
        Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void SetLanguage_SameEnglishLanguage_ReappliesCultureWithoutRaisingSecondEvent()
    {
        var service = CreateService();
        var eventCount = 0;
        service.LanguageChanged += () => eventCount++;

        service.SetLanguage("en");
        service.SetLanguage("en");

        Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
        Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void AvailableLanguages_ContainsViAndEn()
    {
        var service = CreateService();

        var codes = service.AvailableLanguages.Select(l => l.Code).ToList();

        Assert.Contains("vi", codes);
        Assert.Contains("en", codes);
    }
}
