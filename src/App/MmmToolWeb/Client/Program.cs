using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MmmToolWeb.Client;
using MmmToolWeb.Client.Auth;
using MmmToolWeb.Features.Client;
using MmmToolWeb.Messages.Client;
using MmmToolWeb.Notes.Client;
using MmmToolWeb.Notifications.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<PasskeyBrowser>();
builder.Services.AddScoped<AuthClient>();

// 機能。足すときに、画面の本体で書くのは、この 1 行だけ。ホーム画面のタイルは、登録した順に並ぶ
builder.Services.AddClientFeatureSupport();
builder.Services.AddNotesClient();
builder.Services.AddMessagesClient();
builder.Services.AddNotificationsClient();

await builder.Build().RunAsync();
