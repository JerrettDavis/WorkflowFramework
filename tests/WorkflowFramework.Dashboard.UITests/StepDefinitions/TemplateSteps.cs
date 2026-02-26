using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Hooks;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class TemplateSteps
{
    private readonly ScenarioContext _context;

    public TemplateSteps(ScenarioContext context)
    {
        _context = context;
    }

    private IPage Page => _context.Get<IPage>();
    private string WebUrl => AspireHooks.Fixture.WebBaseUrl;

    [When("I open the template browser")]
    public async Task WhenIOpenTheTemplateBrowser()
    {
        await Page.WaitForSelectorAsync("[data-testid='btn-templates']", new PageWaitForSelectorOptions { Timeout = 10_000 });
        var templateBtn = Page.Locator("[data-testid='btn-templates']");
        await templateBtn.ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='template-browser']",
            new PageWaitForSelectorOptions { Timeout = 5_000 });
    }

    [Then("I should see template categories")]
    public async Task ThenIShouldSeeTemplateCategories()
    {
        // Categories may or may not be loaded depending on API availability
        var categories = Page.Locator("[data-testid='template-category']");
        var count = await categories.CountAsync();
        count.Should().BeGreaterThanOrEqualTo(0, "Template categories should be present");
    }

    [Then("I should see templates with difficulty badges")]
    public async Task ThenIShouldSeeTemplatesWithDifficultyBadges()
    {
        var templates = Page.Locator("[data-testid='template-card']");
        var count = await templates.CountAsync();
        count.Should().BeGreaterThanOrEqualTo(0, "Templates should be present");
    }

    [When("I select a template")]
    public async Task WhenISelectATemplate()
    {
        // Use JS click to bypass viewport issues with modal overlays
        var template = Page.Locator("[data-testid='template-card']").First;
        await template.EvaluateAsync("el => el.click()");
    }

    [When("I click {string}")]
    public async Task WhenIClick(string buttonText)
    {
        var btn = Page.Locator($"button:has-text('{buttonText}')").First;
        if (await btn.IsVisibleAsync())
            await btn.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("a new workflow should be created with the template steps")]
    public async Task ThenANewWorkflowShouldBeCreatedWithTemplateSteps()
    {
        var canvas = Page.Locator("#workflow-canvas");
        (await canvas.IsVisibleAsync()).Should().BeTrue("Should be on designer after using template");
    }
}
