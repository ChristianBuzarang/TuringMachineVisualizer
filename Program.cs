using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Supabase;
using TuringMachineVisualizer;
using TuringMachineVisualizer.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});
builder.Services.AddScoped<LocalStorageSessionHandler>();

builder.Services.AddScoped(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var url = config["Supabase:Url"] ?? throw new InvalidOperationException("Missing Supabase:Url");
    var key =
        config["Supabase:AnonKey"]
        ?? throw new InvalidOperationException("Missing Supabase:AnonKey");
    var sessionHandler = sp.GetRequiredService<LocalStorageSessionHandler>();

    var options = new SupabaseOptions
    {
        AutoConnectRealtime = false,
        SessionHandler = sessionHandler,
    };

    return new Client(url, key, options);
});

builder.Services.AddScoped<SupabaseService>();

var host = builder.Build();

var supabase = host.Services.GetRequiredService<SupabaseService>();
await supabase.InitializeAsync();

await host.RunAsync();
