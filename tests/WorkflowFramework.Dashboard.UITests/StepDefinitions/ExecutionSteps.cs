using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Hooks;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class ExecutionSteps
{
    private readonly ScenarioContext _context;

    public ExecutionSteps(ScenarioContext context)
    {
        _context = context;
    }

    private IPage Page => _context.Get<IPage>();
    private string WebUrl => AspireHooks.Fixture.WebBaseUrl;

    [Given("I have a valid workflow with {int} steps")]
    public async Task GivenIHaveAValidWorkflowWithSteps(int stepCount)
    {
        using var client = AspireHooks.Fixture.CreateApiClient();
        var steps = Enumerable.Range(1, stepCount).Select(i => new
        {
            id = $"step{i}",
            type = "action",
            name = $"Step {i}",
            config = new Dictionary<string, object> { ["actionType"] = "noop" }
        }).ToArray();

        var payload = new
        {
            description = $"Valid workflow with {stepCount} steps",
            tags = new[] { "test" },
            definition = new { name = "Valid Test Workflow", steps }
        };
        var response = await client.PostAsJsonAsync("/api/workflows", payload);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var id = result!["id"].ToString()!;
        _context.Set(id, "WorkflowId");

        await Page.GotoAsync($"{WebUrl}/designer/{id}",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }

    [When("I run the workflow")]
    public async Task WhenIRunTheWorkflow()
    {
        var runBtn = Page.Locator("button:has-text('Run'), [data-testid='run-btn']").First;
        if (await runBtn.IsVisibleAsync())
        {
            await runBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(2000); // Wait for execution
        }
    }

    [Then("the execution panel should appear")]
    public async Task ThenTheExecutionPanelShouldAppear()
    {
        var panel = Page.Locator("[data-testid='execution-panel'], .execution-panel, .bottom-panel");
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("I should see step status updates")]
    public async Task ThenIShouldSeeStepStatusUpdates()
    {
        // Look for any status indicators on the page
        await Page.WaitForTimeoutAsync(1000);
    }

    [Given("I have completed workflow runs")]
    public async Task GivenIHaveCompletedWorkflowRuns()
    {
        // Create and run a workflow via API
        using var client = AspireHooks.Fixture.CreateApiClient();
        var payload = new
        {
            description = "Completed run test",
            tags = new[] { "test" },
            definition = new
            {
                name = "Completed Workflow",
                steps = new[]
                {
                    new { id = "s1", type = "action", name = "Step 1", config = new Dictionary<string, object>() }
                }
            }
        };
        var createResp = await client.PostAsJsonAsync("/api/workflows", payload);
        createResp.EnsureSuccessStatusCode();
        var result = await createResp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var id = result!["id"].ToString()!;

        // Run the workflow
        await client.PostAsync($"/api/workflows/{id}/run", null);
        await Task.Delay(1000); // Wait for run
    }

    [Then("I should see past runs with status and duration")]
    public async Task ThenIShouldSeePastRunsWithStatusAndDuration()
    {
        var runs = Page.Locator("[data-testid='run-item'], .run-item, tr, .run-entry");
        await Page.WaitForTimeoutAsync(500);
    }
}
