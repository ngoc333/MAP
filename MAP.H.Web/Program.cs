using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Navigation;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Modules;
using MAP.C.Contract.Database;
using MAP.C.Contract.Logging;
using MAP.C.Runtime.Navigation;
using MAP.C.Runtime.Database;
using MAP.C.Runtime.Localization;
using MAP.C.UI.Headers;
using MAP.C.UI.Localization;
using MAP.C.Wasm.Menus;
using MAP.C.Wasm.Modules;
using MAP.C.Wasm.Logging;
using MAP.H.Web;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddSingleton<IndexedDbLogStore>();
builder.Services.AddSingleton<ILogStore>(sp => sp.GetRequiredService<IndexedDbLogStore>());
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.Services.AddSingleton<ILoggerProvider, IndexedDbLoggerProvider>();

await using var dbApiConfigurationStream = await http.GetStreamAsync("db-api.json");
var dbApiConfiguration = await DbApiConfiguration.LoadAsync(dbApiConfigurationStream);
builder.Services.AddSingleton<IDbApiClient>(_ => new DbApiClient(new HttpClient
{
    BaseAddress = dbApiConfiguration.OracleBaseAddress,
    Timeout = TimeSpan.FromSeconds(10)
}, new HttpClient
{
    BaseAddress = dbApiConfiguration.PostgreSqlBaseAddress,
    Timeout = TimeSpan.FromSeconds(10)
}));

var loader = new ResourceLoader();
var langService = new JsonLanguageService(loader);
await langService.InitializeAsync(typeof(JsonLanguageService).Assembly);
builder.Services.AddSingleton<IResourceLoader>(loader);
builder.Services.AddSingleton<ILanguageService>(langService);
builder.Services.AddSingleton<Radzen.ILocalizer, RadzenLocalizer>();

builder.Services.AddScoped<IModuleLoader, ModuleLoader>();
builder.Services.AddScoped<IPageNavigator, PageNavigator>();
builder.Services.AddScoped<IPageHeaderState, PageHeaderState>();

builder.Services.AddRadzenComponents();

var host = builder.Build();
host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AppStartup").LogInformation(
    "Web application starting. SessionId={SessionId} BaseAddress={BaseAddress}",
    DiagnosticContext.SessionId, builder.HostEnvironment.BaseAddress);
await host.RunAsync();
