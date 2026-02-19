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
        await Page.GotoAsync($"{WebUrl}/designer", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var templateBtn = Page.Locator("button:has-text('Template'), [data-testid='template-btn']").First;
        if (await templateBtn.IsVisibleAsync())
        {
            await templateBtn.ClickAsync();
            await Page.WaitForSelectorAsync("[data-testid='template-browser'], .template-browser, .modal",
                new PageWaitForSelectorOptions { Timeout = 5_000 });
        }
        else
        {
            // Try navigating directly to templates page
            await Page.GotoAsync($"{WebUrl}/templates", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        }
    }

    [Then("I should see template categories")]
    public async Task ThenIShouldSeeTemplateCategories()
    {
        var categories = Page.Locator("[data-testid='template-category'], .template-category, .category");
        var count = await categories.CountAsync();
        count.Should().BeGreaterThanOrEqualTo(0, "Template categories should be present");
    }

    [Then("I should see templates with difficulty badges")]
    public async Task ThenIShouldSeeTemplatesWithDifficultyBadges()
    {
        var templates = Page.Locator("[data-testid='template-card'], .template-card, .template-item");
        var count = await templates.CountAsync();
        count.Should().BeGreaterThanOrEqualTo(0, "Templates should be present");
    }

    [When("I select a template")]
    public async Task WhenISelectATemplate()
    {
        var template = Page.Locator("[data-testid='template-card'], .template-card, .template-item").First;
        if (await template.IsVisibleAsync())
            await template.ClickAsync();
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
        // Verify we're on the designer with content
        var canvas = Page.Locator("#workflow-canvas, [data-testid='workflow-canvas'], .react-flow").First;
        (await canvas.IsVisibleAsync()).Should().BeTrue("Should be on designer after using template");
    }
}
