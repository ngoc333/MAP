using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MAP.C.Contract.Config;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Logging;
using MAP.C.Runtime.Database;
using MAP.C.Runtime.Localization;
using MAP.C.Wasm;
using MAP.C.Wasm.Config;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register root components using assembly type lookup
var appType = typeof(MAP.H.Web._Imports).Assembly.GetTypes()
    .FirstOrDefault(t => t.Name == "App")
    ?? throw new InvalidOperationException("App component not found in MAP.H.Web assembly");
builder.RootComponents.Add(appType, "#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };

// Register Wasm platform services
builder.Services.AddWasm(http);

// Register database API configuration
await using var dbApiConfigurationStream = await http.GetStreamAsync("db-api.json");
var dbApiConfiguration = await DbApiConfiguration.LoadAsync(dbApiConfigurationStream);
builder.Services.AddWasmDbApi(dbApiConfiguration);

var host = builder.Build();

// Initialize language service
var langService = host.Services.GetRequiredService<ILanguageService>();
if (langService is JsonLanguageService jsonLang)
    await jsonLang.InitializeAsync(typeof(JsonLanguageService).Assembly);

// Log startup
host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AppStartup").LogInformation(
    "Web application starting. SessionId={SessionId} BaseAddress={BaseAddress}",
    DiagnosticContext.SessionId, builder.HostEnvironment.BaseAddress);

// Load configuration
await host.Services.GetRequiredService<AppConfigService>().LoadAsync();

await host.RunAsync();
