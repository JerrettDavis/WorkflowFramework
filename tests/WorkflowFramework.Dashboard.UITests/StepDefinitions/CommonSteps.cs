using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Hooks;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class CommonSteps
{
    private readonly ScenarioContext _context;

    public CommonSteps(ScenarioContext context)
    {
        _context = context;
    }

    private IPage Page => _context.Get<IPage>();
    private string WebUrl => AspireHooks.Fixture.WebBaseUrl;
    private string ApiUrl => AspireHooks.Fixture.ApiBaseUrl;

    [Given("the dashboard is running")]
    public async Task GivenTheDashboardIsRunning()
    {
        var response = await Page.GotoAsync(WebUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 90_000
        });
        response.Should().NotBeNull();
        response!.Ok.Should().BeTrue();
        // Wait for Blazor circuit to connect and render the toolbar
        await Page.WaitForSelectorAsync("[data-testid='toolbar']",
            new PageWaitForSelectorOptions { Timeout = 30_000 });
        // Wait for Blazor Server circuit to become interactive.
        // Prerendered HTML has the toolbar but buttons don't work until
        // the SignalR circuit connects and Blazor re-renders interactively.
        await WaitForBlazorInteractiveAsync();
    }

    /// <summary>
    /// Waits for Blazor Server interactive mode by checking that the
    /// Blazor circuit is connected (the _blazorInitialized marker or
    /// by verifying a click handler is wired up).
    /// </summary>
    private async Task WaitForBlazorInteractiveAsync()
    {
        // Blazor Server sets up a SignalR connection. We can detect it by
        // waiting for the Blazor._internal object to exist, or more reliably,
        // by waiting for the blazor-enhanced attribute to appear on re-render.
        // Simplest: wait for the WebSocket connection via Blazor's circuit marker.
        await Page.WaitForFunctionAsync(
            "() => document.querySelector('[data-server-rendered]') === null || window.Blazor !== undefined",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        // Additional brief pause to let Blazor finish re-rendering after circuit connect
        await Page.WaitForTimeoutAsync(500);
    }

    [When("I navigate to the designer")]
    public async Task WhenINavigateToTheDesigner()
    {
        await EnsureOnDesignerAsync();
        await Page.WaitForSelectorAsync("#workflow-canvas",
            new PageWaitForSelectorOptions { Timeout = 30_000 });
    }

    [When("I navigate to run history")]
    public async Task WhenINavigateToRunHistory()
    {
        await EnsureOnDesignerAsync();
        await Page.Locator("[data-testid='tab-output']").ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='output-content']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    /// <summary>
    /// Ensures the page is on the designer (root). Only navigates if not already there.
    /// This avoids creating redundant Blazor circuits.
    /// </summary>
    private async Task EnsureOnDesignerAsync()
    {
        var toolbar = Page.Locator("[data-testid='toolbar']");
        if (await toolbar.IsVisibleAsync())
            return; // Already on the designer

        await Page.GotoAsync(WebUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 90_000
        });
        await Page.WaitForSelectorAsync("[data-testid='toolbar']",
            new PageWaitForSelectorOptions { Timeout = 30_000 });
        await WaitForBlazorInteractiveAsync();
    }

    /// <summary>
    /// Creates a workflow via API and stores its ID in context.
    /// </summary>
    private async Task<string> CreateWorkflowViaApiAsync(string name, object? definition = null)
    {
        using var client = AspireHooks.Fixture.CreateApiClient();
        var payload = new
        {
            description = $"Test workflow: {name}",
            tags = new[] { "test" },
            definition = definition ?? new
            {
                name,
                steps = new[]
                {
                    new { id = "step1", type = "action", name = "Step 1", config = new Dictionary<string, object>() }
                }
            }
        };
        var response = await client.PostAsJsonAsync("/api/workflows", payload);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var id = result!["id"].ToString()!;
        _context.Set(id, "WorkflowId");
        return id;
    }
}
