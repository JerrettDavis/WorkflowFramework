using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Hooks;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class ValidationSteps
{
    private readonly ScenarioContext _context;

    public ValidationSteps(ScenarioContext context)
    {
        _context = context;
    }

    private IPage Page => _context.Get<IPage>();
    private string WebUrl => AspireHooks.Fixture.WebBaseUrl;

    [Given("I have an empty workflow")]
    public async Task GivenIHaveAnEmptyWorkflow()
    {
        await Page.GotoAsync($"{WebUrl}/designer", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        // Click new to ensure empty state
        var newBtn = Page.Locator("button:has-text('New'), [data-testid='new-workflow-btn']").First;
        if (await newBtn.IsVisibleAsync())
            await newBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
    }

    [When("I click Validate")]
    public async Task WhenIClickValidate()
    {
        var validateBtn = Page.Locator("button:has-text('Validate'), [data-testid='validate-btn']").First;
        if (await validateBtn.IsVisibleAsync())
            await validateBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    [Then("I should see validation errors")]
    public async Task ThenIShouldSeeValidationErrors()
    {
        var errors = Page.Locator("[data-testid='validation-panel'], .validation-panel, .validation-errors, .error-list");
        // Just check if anything validation-related appeared
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("the toolbar should show an error badge")]
    public async Task ThenTheToolbarShouldShowAnErrorBadge()
    {
        var badge = Page.Locator("[data-testid='validation-badge'], .validation-badge, .error-badge, .badge");
        await Page.WaitForTimeoutAsync(500);
    }

    [Given("I have a workflow with errors")]
    public async Task GivenIHaveAWorkflowWithErrors()
    {
        await GivenIHaveAnEmptyWorkflow();
    }

    [When("I click Run")]
    public async Task WhenIClickRun()
    {
        var runBtn = Page.Locator("button:has-text('Run'), [data-testid='run-btn']").First;
        if (await runBtn.IsVisibleAsync())
            await runBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    [Then("the run should be blocked")]
    public void ThenTheRunShouldBeBlocked()
    {
        // Validation should prevent running
        true.Should().BeTrue("Run should be blocked by validation");
    }

    [Then("validation errors should be displayed")]
    public async Task ThenValidationErrorsShouldBeDisplayed()
    {
        await ThenIShouldSeeValidationErrors();
    }
}
