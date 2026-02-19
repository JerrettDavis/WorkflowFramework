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
        // Verify the web app is reachable
        var response = await Page.GotoAsync(WebUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        response.Should().NotBeNull();
        response!.Ok.Should().BeTrue();
    }

    [When("I navigate to the designer")]
    public async Task WhenINavigateToTheDesigner()
    {
        await Page.GotoAsync($"{WebUrl}/designer", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        // Wait for canvas to load
        await Page.WaitForSelectorAsync("#workflow-canvas, [data-testid='workflow-canvas'], .react-flow",
            new PageWaitForSelectorOptions { Timeout = 15_000 });
    }

    [When("I navigate to run history")]
    public async Task WhenINavigateToRunHistory()
    {
        await Page.GotoAsync($"{WebUrl}/runs", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForSelectorAsync("[data-testid='run-history'], .run-history, table",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    /// <summary>
    /// Creates a workflow via API and stores its ID in context.
    /// </summary>
    protected async Task<string> CreateWorkflowViaApiAsync(string name, object? definition = null)
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
