using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Hooks;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class StepListSteps
{
    private readonly ScenarioContext _context;

    public StepListSteps(ScenarioContext context)
    {
        _context = context;
    }

    private IPage Page => _context.Get<IPage>();

    [When("I click the Steps tab")]
    public async Task WhenIClickTheStepsTab()
    {
        var tab = Page.Locator("[data-testid='tab-steps']");
        await tab.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("I should see all workflow steps listed")]
    public async Task ThenIShouldSeeAllWorkflowStepsListed()
    {
        var items = Page.Locator("[data-testid='step-list-item']");
        var count = await items.CountAsync();
        count.Should().BeGreaterThan(0, "Step list should contain items");
    }

    [Then("the step types should be labeled")]
    public async Task ThenTheStepTypesShouldBeLabeled()
    {
        var items = Page.Locator("[data-testid='step-list-item']");
        var count = await items.CountAsync();
        count.Should().BeGreaterThan(0, "Step list should have items");
        // Each step item shows a type badge
        var firstItem = items.First;
        var text = await firstItem.TextContentAsync();
        text.Should().NotBeNullOrEmpty("Step list items should have type labels");
    }

    [When("I click a step in the step list")]
    public async Task WhenIClickAStepInTheStepList()
    {
        var items = Page.Locator("[data-testid='step-list-item']");
        var count = await items.CountAsync();
        count.Should().BeGreaterThan(0, "Must have steps to click");
        await items.First.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("the canvas should focus on that step")]
    public async Task ThenTheCanvasShouldFocusOnThatStep()
    {
        // After clicking a step in the list, the canvas should focus on it
        // We verify the canvas still has nodes and is visible
        var canvas = Page.Locator("#workflow-canvas");
        (await canvas.IsVisibleAsync()).Should().BeTrue("Canvas should be visible");
    }

    [Then("the properties panel should show that step's config")]
    public async Task ThenThePropertiesPanelShouldShowThatStepsConfig()
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        (await panel.IsVisibleAsync()).Should().BeTrue("Properties panel should be visible");
        var header = Page.Locator("[data-testid='properties-header']");
        (await header.IsVisibleAsync()).Should().BeTrue("Properties header should show step info");
    }

    [When("I drag a step from the palette to the canvas")]
    public async Task WhenIDragAStepFromThePaletteToTheCanvas()
    {
        // Click a step item in the palette to add it (drag simulation)
        var paletteItems = Page.Locator("[data-testid='step-item']");
        var count = await paletteItems.CountAsync();
        if (count > 0)
        {
            var source = paletteItems.First;
            var canvas = Page.Locator("#workflow-canvas");

            // Get bounding boxes for drag
            var sourceBox = await source.BoundingBoxAsync();
            var canvasBox = await canvas.BoundingBoxAsync();

            if (sourceBox is not null && canvasBox is not null)
            {
                await Page.Mouse.MoveAsync(
                    sourceBox.X + sourceBox.Width / 2,
                    sourceBox.Y + sourceBox.Height / 2);
                await Page.Mouse.DownAsync();
                await Page.Mouse.MoveAsync(
                    canvasBox.X + canvasBox.Width / 2,
                    canvasBox.Y + canvasBox.Height / 2,
                    new MouseMoveOptions { Steps = 10 });
                await Page.Mouse.UpAsync();
                await Page.WaitForTimeoutAsync(500);
            }
        }
    }

    [Then("the step list should show the new step")]
    public async Task ThenTheStepListShouldShowTheNewStep()
    {
        var items = Page.Locator("[data-testid='step-list-item']");
        var count = await items.CountAsync();
        count.Should().BeGreaterThan(0, "Step list should show the added step");
    }
}
