using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MAP.C.Contract.Services;
using MAP.C.Runtime.Services;
using MAP.H.Web;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IModuleLoader, ModuleLoader>();
builder.Services.AddRadzenComponents();

await builder.Build().RunAsync();
