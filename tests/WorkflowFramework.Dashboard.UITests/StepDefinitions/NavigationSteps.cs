using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Hooks;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class NavigationSteps
{
    private readonly ScenarioContext _context;

    public NavigationSteps(ScenarioContext context)
    {
        _context = context;
    }

    private IPage Page => _context.Get<IPage>();
    private string WebUrl => AspireHooks.Fixture.WebBaseUrl;

    [When("I click the Designer nav link")]
    public async Task WhenIClickTheDesignerNavLink()
    {
        var link = Page.Locator("[data-testid='nav-designer']");
        await link.ClickAsync();
        await Page.WaitForSelectorAsync("#workflow-canvas",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    [Then("I should be on the settings page")]
    public async Task ThenIShouldBeOnTheSettingsPage()
    {
        var url = Page.Url;
        url.Should().Contain("/settings", "URL should be the settings page");
        var settingsPage = Page.Locator("[data-testid='settings-page']");
        (await settingsPage.IsVisibleAsync()).Should().BeTrue("Settings page should be visible");
    }

    [Then("I should be on the designer page")]
    public async Task ThenIShouldBeOnTheDesignerPage()
    {
        var canvas = Page.Locator("#workflow-canvas");
        (await canvas.IsVisibleAsync()).Should().BeTrue("Canvas should be visible on designer page");
    }

    [When("I click the back arrow")]
    public async Task WhenIClickTheBackArrow()
    {
        var backBtn = Page.Locator("[data-testid='settings-back']");
        await backBtn.ClickAsync();
        await Page.WaitForSelectorAsync("#workflow-canvas",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
    }
}
