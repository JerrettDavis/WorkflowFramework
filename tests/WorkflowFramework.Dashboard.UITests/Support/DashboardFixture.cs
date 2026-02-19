using Aspire.Hosting;
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

        builder.Services.ConfigureHttpClientDefaults(http =>
            http.AddStandardResilienceHandler());

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        // Wait for resources to be running
        var rns = _app.ResourceNotifications;
        await rns.WaitForResourceAsync("dashboard-api")
            .WaitAsync(TimeSpan.FromMinutes(2));
        await rns.WaitForResourceAsync("dashboard-web")
            .WaitAsync(TimeSpan.FromMinutes(2));
        // Brief delay for endpoints to stabilize
        await Task.Delay(2000);

        WebBaseUrl = GetEndpointSafe("dashboard-web");
        ApiBaseUrl = GetEndpointSafe("dashboard-api");
    }

    private string GetEndpointSafe(string resourceName)
    {
        try { return _app!.GetEndpoint(resourceName, "https").ToString(); }
        catch { }
        try { return _app!.GetEndpoint(resourceName, "http").ToString(); }
        catch { }
        // Fallback: try without endpoint name
        return _app!.GetEndpoint(resourceName).ToString();
    }

    public HttpClient CreateApiClient()
    {
        return new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };
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
