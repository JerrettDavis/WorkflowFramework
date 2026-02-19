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

        // Wait for resources to be ready
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("dashboard-web")
            .WaitAsync(TimeSpan.FromMinutes(2));
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("dashboard-api")
            .WaitAsync(TimeSpan.FromMinutes(2));

        WebBaseUrl = _app.GetEndpoint("dashboard-web", "https")?.ToString()
                     ?? _app.GetEndpoint("dashboard-web", "http")!.ToString();
        ApiBaseUrl = _app.GetEndpoint("dashboard-api", "https")?.ToString()
                     ?? _app.GetEndpoint("dashboard-api", "http")!.ToString();
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
