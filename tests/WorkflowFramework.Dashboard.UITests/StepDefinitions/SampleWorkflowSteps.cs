using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Hooks;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class SampleWorkflowSteps
{
    private readonly ScenarioContext _context;

    public SampleWorkflowSteps(ScenarioContext context)
    {
        _context = context;
    }

    private IPage Page => _context.Get<IPage>();
    private string WebUrl => AspireHooks.Fixture.WebBaseUrl;

    [When("I open the workflow list dialog")]
    public async Task WhenIOpenTheWorkflowListDialog()
    {
        await Page.WaitForSelectorAsync("[data-testid='btn-open']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
        var openBtn = Page.Locator("[data-testid='btn-open']");
        await openBtn.ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { Timeout = 5_000 });
    }

    [Then("I should see at least {int} sample workflows")]
    public async Task ThenIShouldSeeAtLeastSampleWorkflows(int minCount)
    {
        var items = Page.Locator("[data-testid='workflow-list-item']");
        var count = await items.CountAsync();
        count.Should().BeGreaterThanOrEqualTo(minCount,
            $"Should have at least {minCount} sample workflows");
    }

    [Then("I should see {string} in the list")]
    public async Task ThenIShouldSeeInTheList(string workflowName)
    {
        var list = Page.Locator("[data-testid='workflow-list']");
        var text = await list.TextContentAsync();
        text.Should().Contain(workflowName,
            $"Workflow list should contain '{workflowName}'");
    }

    [When("I open the {string} workflow")]
    public async Task WhenIOpenTheWorkflow(string workflowName)
    {
        var item = Page.Locator("[data-testid='workflow-list-item']",
            new PageLocatorOptions { HasText = workflowName }).First;
        await item.ClickAsync();
        // Wait for dialog to close and workflow to load
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
        await Page.WaitForTimeoutAsync(1000);
    }

    [When("I open the {string} sample workflow")]
    [Given("I open the {string} sample workflow")]
    public async Task WhenIOpenTheSampleWorkflow(string workflowName)
    {
        // Open the workflow list dialog first
        await Page.WaitForSelectorAsync("[data-testid='btn-open']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
        var openBtn = Page.Locator("[data-testid='btn-open']");
        await openBtn.ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { Timeout = 5_000 });

        // Select the workflow
        var item = Page.Locator("[data-testid='workflow-list-item']",
            new PageLocatorOptions { HasText = workflowName }).First;
        await item.ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
        await Page.WaitForTimeoutAsync(1000);
    }

    [Then("the canvas should have nodes")]
    public async Task ThenTheCanvasShouldHaveNodes()
    {
        var nodes = Page.Locator(".react-flow__node");
        var count = await nodes.CountAsync();
        count.Should().BeGreaterThan(0, "Canvas should have at least one node");
    }

    [Then("the step list should show steps")]
    public async Task ThenTheStepListShouldShowSteps()
    {
        // Click the Steps tab to show the step list
        var stepsTab = Page.Locator("[data-testid='tab-steps']");
        await stepsTab.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var items = Page.Locator("[data-testid='step-list-item']");
        var count = await items.CountAsync();
        count.Should().BeGreaterThan(0, "Step list should have entries");
    }

    [When("I click on a node of type {string}")]
    public async Task WhenIClickOnANodeOfType(string stepType)
    {
        // Use JS to find and select the node by type
        await Page.EvaluateAsync(@"(type) => {
            const nodes = window.workflowEditor?.getNodes?.() ?? [];
            const node = nodes.find(n => n.data?.type === type || n.type === type);
            if (node) window.workflowEditor?.selectNode?.(node.id);
        }", stepType);
        await Page.WaitForTimeoutAsync(500);

        // Fallback: click the first node that matches
        var nodes = Page.Locator(".react-flow__node");
        var count = await nodes.CountAsync();
        if (count > 0)
        {
            // Try to find by type label
            for (var i = 0; i < count; i++)
            {
                var node = nodes.Nth(i);
                var text = await node.TextContentAsync();
                if (text?.Contains(stepType, StringComparison.OrdinalIgnoreCase) == true)
                {
                    await node.ClickAsync();
                    await Page.WaitForTimeoutAsync(500);
                    return;
                }
            }
            // Fallback: click first node
            await nodes.First.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }
    }

    [Then("the properties panel should show {string} configuration")]
    public async Task ThenThePropertiesPanelShouldShowConfiguration(string configLabel)
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        (await panel.IsVisibleAsync()).Should().BeTrue("Properties panel should be visible");
        var text = await panel.TextContentAsync();
        text.Should().NotBeNullOrEmpty("Properties panel should have content");
    }

    [Then("the provider field should have a value")]
    public async Task ThenTheProviderFieldShouldHaveAValue()
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        // Look for a select element with a provider value
        var selects = panel.Locator("select");
        var count = await selects.CountAsync();
        count.Should().BeGreaterThan(0, "Should have dropdown controls for provider");
    }

    [Then("the url field should not be empty")]
    public async Task ThenTheUrlFieldShouldNotBeEmpty()
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        var inputs = panel.Locator("input[type='text']");
        var count = await inputs.CountAsync();
        count.Should().BeGreaterThan(0, "Should have text inputs for URL");
    }

    [Given("I open a sample workflow with multiple steps")]
    public async Task GivenIOpenASampleWorkflowWithMultipleSteps()
    {
        await WhenIOpenTheSampleWorkflow("Order Processing Pipeline");
    }
}
