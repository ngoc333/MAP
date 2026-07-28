using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MAP.C.Contract.Navigation;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Modules;
using MAP.C.Contract.UI.Headers;
using MAP.C.Runtime.Navigation;
using MAP.C.Runtime.UI.Headers;
using MAP.C.Wasm.Menus;
using MAP.C.Wasm.Modules;
using MAP.H.Web;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IModuleLoader, ModuleLoader>();
builder.Services.AddScoped<IPageNavigator, PageNavigator>();
builder.Services.AddScoped<IPageHeaderState, PageHeaderState>();
builder.Services.AddRadzenComponents();

await builder.Build().RunAsync();
