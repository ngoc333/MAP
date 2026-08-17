using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using MAP.C.Contract.Config;
using MAP.C.Contract.Context;
using MAP.C.Contract.Database;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Models;
using MAP.C.Runtime.Menus;
using Microsoft.Extensions.Logging;

namespace MAP.C.Wpf.Menus;

public sealed class MenuService : IMenuService
{
    private static readonly JsonSerializerOptions JsonReadOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly JsonSerializerOptions JsonWriteOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

    private readonly IDbApiClient _dbClient;
    private readonly IAppConfigService _configService;
    private readonly ILogger<MenuService> _logger;
    private readonly IClientContextService _context;
    private PageConfig? _config;

    private static string LocalConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "page.json");

    public List<MenuItem> Menus => _config?.Menus ?? new();
    public string? StartPageId => _config?.StartPageId;
    public string? DbName => _config?.DbName;
    public bool SupportsLocalConfigEditing => true;
    public string? LocalConfigLocation => LocalConfigPath;
    public event Action? OnMenusLoaded;

    public MenuService(
        IDbApiClient dbClient,
        IAppConfigService configService,
        IClientContextService context,
        ILogger<MenuService> logger)
    {
        _dbClient = dbClient;
        _configService = configService;
        _context = context;
        _logger = logger;
    }

    public async Task LoadMenusAsync()
    {
        if (_config is not null)
            return;

        var started = Stopwatch.GetTimestamp();
        var path = LocalConfigPath;
        _logger.LogDebug(
            "Loading WPF menu. Path={Path} Exists={Exists}",
            path,
            File.Exists(path));

        PageConfig localConfig;
        try
        {
            using var stream = File.OpenRead(path);
            localConfig = JsonSerializer.Deserialize<PageConfig>(stream, JsonReadOptions)
                ?? throw new InvalidOperationException(
                    "Menu configuration deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Invalid menu configuration '{path}': {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load menu configuration '{path}': {ex.Message}", ex);
        }

        _config = await MenuConfigResolver.ResolveAsync(
            localConfig,
            _configService,
            _dbClient,
            _logger,
            started,
            _context.Current);

        OnMenusLoaded?.Invoke();
    }

    public async Task<string?> ReadLocalConfigAsync()
    {
        var path = LocalConfigPath;
        if (!File.Exists(path))
            return null;

        return await File.ReadAllTextAsync(path);
    }

    public async Task SaveLocalConfigAsync(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("page.json cannot be empty.");

        var path = LocalConfigPath;
        PageConfig config;

        try
        {
            config = JsonSerializer.Deserialize<PageConfig>(json, JsonReadOptions)
                ?? throw new InvalidOperationException(
                    "Menu configuration deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Invalid menu configuration '{path}': {ex.Message}", ex);
        }

        MenuConfigValidator.Validate(config);

        var normalized = JsonSerializer.Serialize(config, JsonWriteOptions);
        var tempPath = path + ".tmp";

        try
        {
            await File.WriteAllTextAsync(tempPath, normalized);
            File.Move(tempPath, path, overwrite: true);
            _logger.LogInformation("Local menu configuration saved. Path={Path}", path);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Ignore cleanup errors and preserve the original exception.
                }
            }

            throw;
        }
    }

    public MenuItem? FindById(string id) => MenuTree.Find(Menus, id);
}
