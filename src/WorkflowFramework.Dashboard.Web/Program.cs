using WorkflowFramework.Dashboard.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure SignalR/Blazor circuit options for test reliability
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 512 * 1024; // 512KB
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromSeconds(10);
    options.DisconnectedCircuitMaxRetained = 10;
});

builder.Services.AddHttpClient<DashboardApiClient>(client =>
    client.BaseAddress = new Uri("https+http://dashboard-api"));

builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<KeyboardShortcutService>();
builder.Services.AddScoped<UserPreferencesService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapDefaultEndpoints();

app.MapRazorComponents<WorkflowFramework.Dashboard.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
