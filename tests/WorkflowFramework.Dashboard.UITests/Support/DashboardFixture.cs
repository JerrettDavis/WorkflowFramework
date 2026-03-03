using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace WorkflowFramework.Dashboard.UITests.Support;

/// <summary>
/// Manages the Aspire-hosted Dashboard AppHost lifecycle for testing.
/// </summary>
public sealed class DashboardFixture : IAsyncDisposable
{
    private DistributedApplication? _app;

    public string WebBaseUrl { get; private set; } = string.Empty;
    public string ApiBaseUrl { get; private set; } = string.Empty;

    public async Task StartAsync()
    {
        if (_app is not null) return;

        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.WorkflowFramework_Dashboard_AppHost>();

        // UI tests do not require DB persistence and run more reliably with in-memory stores.
        builder.Configuration["Dashboard:UsePersistence"] = "false";

        builder.Services.ConfigureHttpClientDefaults(http =>
            http.AddStandardResilienceHandler());

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        // Wait for resources to be running
        var rns = _app.ResourceNotifications;
        var awaitedStates = new[]
        {
            KnownResourceStates.Running,
            KnownResourceStates.FailedToStart,
            KnownResourceStates.RuntimeUnhealthy,
            KnownResourceStates.Exited
        };

        var apiState = await rns.WaitForResourceAsync("dashboard-api", awaitedStates)
            .WaitAsync(TimeSpan.FromMinutes(2));
        var webState = await rns.WaitForResourceAsync("dashboard-web", awaitedStates)
            .WaitAsync(TimeSpan.FromMinutes(2));

        if (!string.Equals(apiState, KnownResourceStates.Running, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"dashboard-api entered '{apiState}' instead of '{KnownResourceStates.Running}'.");
        if (!string.Equals(webState, KnownResourceStates.Running, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"dashboard-web entered '{webState}' instead of '{KnownResourceStates.Running}'.");

        using var webClient = CreateResourceClient("dashboard-web");
        using var apiClient = CreateResourceClient("dashboard-api");

        WebBaseUrl = webClient.BaseAddress?.ToString().TrimEnd('/')
            ?? throw new InvalidOperationException("dashboard-web did not expose a base address.");
        ApiBaseUrl = apiClient.BaseAddress?.ToString().TrimEnd('/')
            ?? throw new InvalidOperationException("dashboard-api did not expose a base address.");
    }

    public HttpClient CreateApiClient()
    {
        var client = CreateResourceClient("dashboard-api");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private HttpClient CreateResourceClient(string resourceName)
    {
        try
        {
            return _app!.CreateHttpClient(resourceName, "https");
        }
        catch
        {
            return _app!.CreateHttpClient(resourceName, "http");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
