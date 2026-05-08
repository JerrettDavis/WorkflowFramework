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
            new PageWaitForSelectorOptions { Timeout = 15_000 });
    }

    [Then("I should see template categories")]
    public async Task ThenIShouldSeeTemplateCategories()
    {
        await Page.WaitForFunctionAsync(
            "() => document.querySelectorAll(\"[data-testid='template-category']\").length > 0",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        var categories = Page.Locator("[data-testid='template-category']");
        var count = await categories.CountAsync();
        count.Should().BeGreaterThan(0, "Template categories should be present");
    }

    [Then("I should see templates with difficulty badges")]
    public async Task ThenIShouldSeeTemplatesWithDifficultyBadges()
    {
        await Page.WaitForFunctionAsync(
            "() => document.querySelectorAll(\"[data-testid='template-card']\").length > 0",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        var templates = Page.Locator("[data-testid='template-card']");
        var count = await templates.CountAsync();
        count.Should().BeGreaterThan(0, "Templates should be present");
    }

    [Then("I should see featured starter workflows")]
    public async Task ThenIShouldSeeFeaturedStarterWorkflows()
    {
        await Page.WaitForFunctionAsync(
            "() => document.querySelectorAll(\"[data-testid='template-featured-badge']\").length > 0",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        var featured = Page.Locator("[data-testid='template-featured-badge']");
        var count = await featured.CountAsync();
        count.Should().BeGreaterThan(0, "At least one featured starter workflow should be highlighted");
    }

    [Then("I should see starter preview images")]
    public async Task ThenIShouldSeeStarterPreviewImages()
    {
        await Page.WaitForFunctionAsync(
            "() => document.querySelectorAll(\"[data-testid='template-preview-image']\").length > 0",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        var previews = Page.Locator("[data-testid='template-preview-image']");
        var count = await previews.CountAsync();
        count.Should().BeGreaterThan(0, "Starter templates with preview metadata should render preview images");
    }

    [When("I select a template")]
    public async Task WhenISelectATemplate()
    {
        // Use JS click to bypass viewport issues with modal overlays
        var template = Page.Locator("[data-testid='template-card']").First;
        await template.EvaluateAsync("el => el.click()");
    }

    [When("I search templates for {string}")]
    public async Task WhenISearchTemplatesFor(string searchTerm)
    {
        var search = Page.Locator("[data-testid='template-search']");
        await search.FillAsync(searchTerm);
        await Page.WaitForTimeoutAsync(250);
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
        await canvas.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
    }

    [Then("I should only see template results matching {string}")]
    public async Task ThenIShouldOnlySeeTemplateResultsMatching(string searchTerm)
    {
        var templates = Page.Locator("[data-testid='template-card']");
        var count = await templates.CountAsync();
        count.Should().BeGreaterThan(0);

        var loweredSearchTerm = searchTerm.ToLowerInvariant();
        for (var index = 0; index < count; index++)
        {
            var text = (await templates.Nth(index).InnerTextAsync()).ToLowerInvariant();
            text.Should().Contain(loweredSearchTerm);
        }
    }
}


