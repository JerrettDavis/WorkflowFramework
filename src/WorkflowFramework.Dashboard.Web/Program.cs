using Microsoft.AspNetCore.Components.Server.Circuits;
using WorkflowFramework.Dashboard.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure SignalR/Blazor circuit options
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 512 * 1024; // 512KB
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromSeconds(30);
    options.DisconnectedCircuitMaxRetained = 100;
});

// Track circuit lifecycle for diagnostics
builder.Services.AddSingleton<CircuitStats>();
builder.Services.AddScoped<CircuitHandler, TrackingCircuitHandler>();

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

// Circuit diagnostics endpoint
app.MapGet("/diagnostics/circuits", (CircuitStats stats) => new
{
    stats.TotalCreated,
    stats.TotalDisposed,
    Active = stats.TotalCreated - stats.TotalDisposed,
    stats.TotalDisconnected,
    stats.TotalReconnected,
});

app.MapRazorComponents<WorkflowFramework.Dashboard.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Circuit tracking
public class CircuitStats
{
    public int TotalCreated;
    public int TotalDisposed;
    public int TotalDisconnected;
    public int TotalReconnected;
}

public class TrackingCircuitHandler(CircuitStats stats, ILogger<TrackingCircuitHandler> logger) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken ct)
    {
        var count = Interlocked.Increment(ref stats.TotalCreated);
        var active = count - stats.TotalDisposed;
        logger.LogInformation("Circuit OPENED: {Id} (total={Total}, active={Active})", circuit.Id, count, active);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken ct)
    {
        var count = Interlocked.Increment(ref stats.TotalDisposed);
        var active = stats.TotalCreated - count;
        logger.LogInformation("Circuit CLOSED: {Id} (disposed={Disposed}, active={Active})", circuit.Id, count, active);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken ct)
    {
        Interlocked.Increment(ref stats.TotalDisconnected);
        logger.LogInformation("Circuit DISCONNECTED: {Id}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken ct)
    {
        Interlocked.Increment(ref stats.TotalReconnected);
        logger.LogInformation("Circuit RECONNECTED: {Id}", circuit.Id);
        return Task.CompletedTask;
    }
}
