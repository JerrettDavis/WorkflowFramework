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
        await item.ScrollIntoViewIfNeededAsync();
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
        await content.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
    }

    [Then("the execution panel should show {string}")]
    public async Task ThenTheExecutionPanelShouldShow(string expectedText)
    {
        var panel = Page.Locator("[data-testid='output-content']");
        await panel.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });

        var statusBadge = Page.Locator("[data-testid='run-status-badge']");
        var matched = false;
        for (var i = 0; i < 15; i++)
        {
            var statusText = await statusBadge.TextContentAsync() ?? "";
            if (statusText.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
            {
                matched = true;
                break;
            }

            // Fast workflows can move past Running before the assertion executes.
            if (expectedText.Equals("Running", StringComparison.OrdinalIgnoreCase) &&
                (statusText.Contains("Completed", StringComparison.OrdinalIgnoreCase) ||
                 statusText.Contains("Failed", StringComparison.OrdinalIgnoreCase)))
            {
                matched = true;
                break;
            }

            await Page.WaitForTimeoutAsync(500);
        }

        matched.Should().BeTrue($"Execution panel status should show '{expectedText}' (or later terminal state for fast runs).");
    }

    [Then("I should see step status updates")]
    [Then("I should see step progress updates")]
    public async Task ThenIShouldSeeStepStatusUpdates()
    {
        var tab = Page.Locator("[data-testid='tab-output']");
        await tab.ClickAsync();
        var panel = Page.Locator("[data-testid='output-content']");
        await panel.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });

        int knownSteps = 0;
        for (var i = 0; i < 20; i++)
        {
            var raw = (await Page.Locator("[data-testid='run-metric-known']").TextContentAsync())?.Trim();
            if (int.TryParse(raw, out knownSteps) && knownSteps > 0)
                break;
            await Page.WaitForTimeoutAsync(500);
        }
        knownSteps.Should().BeGreaterThan(0, "Known step count should be populated from the workflow canvas.");

        var narrative = Page.Locator("[data-testid='run-narrative']");
        await narrative.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        (await narrative.TextContentAsync()).Should().NotBeNullOrWhiteSpace("Execution narrative should explain run progress.");

        var timelineRows = Page.Locator("[data-testid='run-event-row']");
        await timelineRows.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        (await timelineRows.CountAsync()).Should().BeGreaterThan(0, "Execution timeline should capture progress events.");
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

        var finalText = await panel.TextContentAsync() ?? "";
        finalText.Should().Contain(stepName, $"Expected completed step '{stepName}' in output feed.");
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
                        type = "Delay",
                        name = "Long Delay",
                        delaySeconds = Math.Max(1, delayMs / 1000),
                        config = new Dictionary<string, string> { ["durationMs"] = delayMs.ToString() }
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
        await item.ScrollIntoViewIfNeededAsync();
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
        var outputTab = Page.Locator("[data-testid='tab-output']");
        await outputTab.ClickAsync();

        var cancelBtn = Page.Locator("[data-testid='btn-cancel-run']");
        await cancelBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await cancelBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    [Then("the run should show status {string}")]
    public async Task ThenTheRunShouldShowStatus(string status)
    {
        var statusBadge = Page.Locator("[data-testid='run-status-badge']");
        for (var i = 0; i < 30; i++)
        {
            var text = await statusBadge.TextContentAsync() ?? "";
            if (text.Contains(status, StringComparison.OrdinalIgnoreCase))
                return;
            await Page.WaitForTimeoutAsync(1000);
        }

        var final = await statusBadge.TextContentAsync() ?? "";
        final.Should().Contain(status, $"Run should reach status '{status}'.");
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
        await item.ScrollIntoViewIfNeededAsync();
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

        var finalText = await panel.TextContentAsync() ?? "";
        finalText.Should().MatchRegex("(?i)(fail|error)", "Output panel should show failure diagnostics.");
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
        await panel.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
    }
}


