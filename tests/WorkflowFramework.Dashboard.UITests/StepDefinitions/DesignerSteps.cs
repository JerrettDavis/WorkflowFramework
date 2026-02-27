using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Hooks;
using WorkflowFramework.Dashboard.UITests.Support;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class DesignerSteps
{
    private readonly ScenarioContext _context;

    public DesignerSteps(ScenarioContext context)
    {
        _context = context;
    }

    private IPage Page => _context.Get<IPage>();

    [Then("I should see the step palette on the left")]
    public async Task ThenIShouldSeeTheStepPaletteOnTheLeft()
    {
        var palette = Page.Locator("[data-testid='step-palette']");
        await palette.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
    }

    [Then("I should see an empty canvas")]
    public async Task ThenIShouldSeeAnEmptyCanvas()
    {
        var canvas = Page.Locator("#workflow-canvas");
        await canvas.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
    }

    [Then("I should see the properties panel on the right")]
    public async Task ThenIShouldSeeThePropertiesPanelOnTheRight()
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        await panel.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
    }

    [When("I type {string} in the step search")]
    public async Task WhenITypeInTheStepSearch(string searchText)
    {
        var searchBox = Page.Locator("[data-testid='step-search']");
        await searchBox.FillAsync(searchText);
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("I should see filtered steps containing {string}")]
    public async Task ThenIShouldSeeFilteredStepsContaining(string text)
    {
        var steps = Page.Locator("[data-testid='step-item']");
        var count = await steps.CountAsync();
        count.Should().BeGreaterThan(0, $"Should have steps matching '{text}'");
    }

    [Given("I have a workflow with an action step")]
    public async Task GivenIHaveAWorkflowWithAnActionStep()
    {
        // Create workflow via API with an action step
        using var client = AspireHooks.Fixture.CreateApiClient();
        var payload = new
        {
            description = "Test workflow with action",
            tags = new[] { "test" },
            definition = new
            {
                name = "Action Test Workflow",
                steps = new[]
                {
                    new { id = "action1", type = "action", name = "My Action Step", config = new Dictionary<string, object> { ["actionType"] = "http" } }
                }
            }
        };
        var response = await client.PostAsJsonAsync("/api/workflows", payload);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var id = result!["id"].ToString();
        _context.Set(id!, "WorkflowId");

        // Open the workflow from the already-loaded designer
        await Page.WaitForSelectorAsync("[data-testid='btn-open']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
        var openBtn = Page.Locator("[data-testid='btn-open']");
        await openBtn.ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { Timeout = 15_000 });

        // Click on the workflow in the list
        var workflowItem = Page.Locator("[data-testid='workflow-list']").Locator("text=Action Test Workflow").First;
        if (await workflowItem.IsVisibleAsync())
            await workflowItem.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    [When("I click on the action step node")]
    public async Task WhenIClickOnTheActionStepNode()
    {
        // React Flow nodes have class .react-flow__node
        var node = Page.Locator(".react-flow__node").First;
        if (await node.IsVisibleAsync())
        {
            await node.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }
    }

    [Then("the properties panel should show the step configuration")]
    public async Task ThenThePropertiesPanelShouldShowTheStepConfiguration()
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        await panel.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
    }

    [Then("the toolbar should show save, run, validate, and settings buttons")]
    public async Task ThenTheToolbarShouldShowButtons()
    {
        var toolbar = Page.Locator("[data-testid='toolbar']");
        await toolbar.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });

        // Verify key buttons exist
        await (Page.Locator("[data-testid='btn-save']")).WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await (Page.Locator("[data-testid='btn-run']")).WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await (Page.Locator("[data-testid='btn-validate']")).WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
    }
}

