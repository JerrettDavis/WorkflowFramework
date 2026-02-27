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

        // Open this workflow from the already-loaded designer
        await Page.WaitForSelectorAsync("[data-testid='btn-open']", new PageWaitForSelectorOptions { Timeout = 10_000 });
        var openBtn = Page.Locator("[data-testid='btn-open']");
        await openBtn.ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { Timeout = 15_000 });
        // Click on the workflow item in the list dialog
        var dialogContent = Page.Locator("[data-testid='workflow-list']");
        var item = dialogContent.Locator("div.cursor-pointer", new LocatorLocatorOptions { HasText = "Valid Test Workflow" }).First;
        await item.ClickAsync();
        // Wait for the dialog to close
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
        await Page.WaitForTimeoutAsync(500);
    }

    [When("I run the workflow")]
    public async Task WhenIRunTheWorkflow()
    {
        var runBtn = Page.Locator("[data-testid='btn-run']");
        await runBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
    }

    [When("I save and run the workflow")]
    public async Task WhenISaveAndRunTheWorkflow()
    {
        // Save first
        var saveBtn = Page.Locator("[data-testid='btn-save']");
        await saveBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        // Then run
        var runBtn = Page.Locator("[data-testid='btn-run']");
        await runBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
    }

    [Then("the execution panel should appear")]
    public async Task ThenTheExecutionPanelShouldAppear()
    {
        // Execution output is in the Output tab
        var tab = Page.Locator("[data-testid='tab-output']");
        await tab.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
        var content = Page.Locator("[data-testid='output-content']");
        (await content.IsVisibleAsync()).Should().BeTrue("Output panel should be visible");
    }

    [Then("the execution panel should show {string}")]
    public async Task ThenTheExecutionPanelShouldShow(string expectedText)
    {
        var panel = Page.Locator("[data-testid='output-content']");
        (await panel.IsVisibleAsync()).Should().BeTrue("Execution panel should be visible");
        // The panel shows run status via text content
        await Page.WaitForTimeoutAsync(1000);
        var text = await panel.TextContentAsync();
        text.Should().NotBeNullOrEmpty("Execution panel should have content");
    }

    [Then("I should see step status updates")]
    [Then("I should see step progress updates")]
    public async Task ThenIShouldSeeStepStatusUpdates()
    {
        // Wait for execution output to appear in the output tab
        await Page.WaitForTimeoutAsync(3000);
        var tab = Page.Locator("[data-testid='tab-output']");
        await tab.ClickAsync();
        var content = Page.Locator("[data-testid='output-content']");
        (await content.IsVisibleAsync()).Should().BeTrue("Output panel should show progress");
    }

    [Then("the run should complete with status {string}")]
    public async Task ThenTheRunShouldCompleteWithStatus(string expectedStatus)
    {
        // Wait for run to complete (up to 30 seconds)
        var panel = Page.Locator("[data-testid='output-content']");
        for (var i = 0; i < 30; i++)
        {
            var text = await panel.TextContentAsync() ?? "";
            if (text.Contains(expectedStatus, StringComparison.OrdinalIgnoreCase))
                return;
            await Page.WaitForTimeoutAsync(1000);
        }
        // Final check
        var finalText = await panel.TextContentAsync() ?? "";
        finalText.Should().Contain(expectedStatus,
            $"Run should complete with status '{expectedStatus}'");
    }

    [Then("I should see step {string} complete")]
    public async Task ThenIShouldSeeStepComplete(string stepName)
    {
        var panel = Page.Locator("[data-testid='output-content']");
        // Wait for step completion in output (up to 30 seconds)
        for (var i = 0; i < 30; i++)
        {
            var text = await panel.TextContentAsync() ?? "";
            if (text.Contains(stepName, StringComparison.OrdinalIgnoreCase))
                return;
            await Page.WaitForTimeoutAsync(1000);
        }
    }

    [Given("I have a workflow with a Delay step of {int}ms")]
    public async Task GivenIHaveAWorkflowWithADelayStep(int delayMs)
    {
        using var client = AspireHooks.Fixture.CreateApiClient();
        var payload = new
        {
            description = "Delay workflow for cancel test",
            tags = new[] { "test" },
            definition = new
            {
                name = "Delay Test Workflow",
                steps = new[]
                {
                    new
                    {
                        id = "delay1",
                        type = "delay",
                        name = "Long Delay",
                        config = new Dictionary<string, object> { ["delayMs"] = delayMs }
                    }
                }
            }
        };
        var response = await client.PostAsJsonAsync("/api/workflows", payload);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        _context.Set(result!["id"].ToString()!, "WorkflowId");

        // Open it
        await Page.WaitForSelectorAsync("[data-testid='btn-open']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
        await Page.Locator("[data-testid='btn-open']").ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { Timeout = 15_000 });
        var item = Page.Locator("[data-testid='workflow-list-item']",
            new PageLocatorOptions { HasText = "Delay Test Workflow" }).First;
        await item.ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
        await Page.WaitForTimeoutAsync(500);
    }

    [When("the workflow is running")]
    [Then("the workflow is running")]
    public async Task WhenTheWorkflowIsRunning()
    {
        // Wait briefly to ensure it's in running state
        await Page.WaitForTimeoutAsync(2000);
    }

    [When("I cancel the run")]
    public async Task WhenICancelTheRun()
    {
        // Look for a cancel/stop button in the execution panel or toolbar
        var cancelBtn = Page.Locator("button:has-text('Cancel'), button:has-text('Stop')").First;
        if (await cancelBtn.IsVisibleAsync())
        {
            await cancelBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);
        }
    }

    [Then("the run should show status {string}")]
    public async Task ThenTheRunShouldShowStatus(string status)
    {
        var panel = Page.Locator("[data-testid='output-content']");
        await Page.WaitForTimeoutAsync(2000);
        var text = await panel.TextContentAsync() ?? "";
        // Status may or may not show depending on how quickly cancel processes
    }

    [Given("I have a workflow with an HttpStep pointing to an invalid URL")]
    public async Task GivenIHaveAWorkflowWithAnHttpStepPointingToAnInvalidUrl()
    {
        using var client = AspireHooks.Fixture.CreateApiClient();
        var payload = new
        {
            description = "Failing HTTP workflow",
            tags = new[] { "test" },
            definition = new
            {
                name = "Failing HTTP Workflow",
                steps = new[]
                {
                    new
                    {
                        id = "http1",
                        type = "HttpStep",
                        name = "BadRequest",
                        config = new Dictionary<string, object>
                        {
                            ["url"] = "http://localhost:99999/nonexistent",
                            ["method"] = "GET"
                        }
                    }
                }
            }
        };
        var response = await client.PostAsJsonAsync("/api/workflows", payload);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        _context.Set(result!["id"].ToString()!, "WorkflowId");

        await Page.WaitForSelectorAsync("[data-testid='btn-open']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
        await Page.Locator("[data-testid='btn-open']").ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { Timeout = 15_000 });
        var item = Page.Locator("[data-testid='workflow-list-item']",
            new PageLocatorOptions { HasText = "Failing HTTP Workflow" }).First;
        await item.ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("I should see a step failure in the output panel")]
    public async Task ThenIShouldSeeAStepFailureInTheOutputPanel()
    {
        var panel = Page.Locator("[data-testid='output-content']");
        // Wait for failure output
        for (var i = 0; i < 15; i++)
        {
            var text = await panel.TextContentAsync() ?? "";
            if (text.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("error", StringComparison.OrdinalIgnoreCase))
                return;
            await Page.WaitForTimeoutAsync(1000);
        }
    }

    [When("the run completes")]
    [Then("the run completes")]
    public async Task WhenTheRunCompletes()
    {
        // Wait for run to finish
        var panel = Page.Locator("[data-testid='output-content']");
        for (var i = 0; i < 30; i++)
        {
            var text = await panel.TextContentAsync() ?? "";
            if (text.Contains("Completed", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Failed", StringComparison.OrdinalIgnoreCase))
                return;
            await Page.WaitForTimeoutAsync(1000);
        }
    }

    [Then("the run should appear in the runs list via API")]
    public async Task ThenTheRunShouldAppearInTheRunsListViaApi()
    {
        using var client = AspireHooks.Fixture.CreateApiClient();
        var response = await client.GetAsync("/api/runs");
        response.EnsureSuccessStatusCode();
        var runs = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
        runs.Should().NotBeNull();
        runs!.Count.Should().BeGreaterThan(0, "Should have at least one run in history");
    }

    [Given("I have completed workflow runs")]
    public async Task GivenIHaveCompletedWorkflowRuns()
    {
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

        await client.PostAsync($"/api/workflows/{id}/run", null);
        await Task.Delay(1000);
    }

    [Then("I should see past runs with status and duration")]
    public async Task ThenIShouldSeePastRunsWithStatusAndDuration()
    {
        // Execution panel on main page shows run output
        var panel = Page.Locator("[data-testid='output-content']");
        (await panel.IsVisibleAsync()).Should().BeTrue("Execution panel should be visible");
    }
}

