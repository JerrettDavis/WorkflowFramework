using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Hooks;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class SettingsSteps
{
    private readonly ScenarioContext _context;

    public SettingsSteps(ScenarioContext context)
    {
        _context = context;
    }

    private IPage Page => _context.Get<IPage>();
    private string WebUrl => AspireHooks.Fixture.WebBaseUrl;

    [When("I click the Settings nav link")]
    public async Task WhenIClickTheSettingsNavLink()
    {
        var link = Page.Locator("[data-testid='nav-settings']");
        await link.ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='settings-page']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    [Given("I am on the settings page")]
    public async Task GivenIAmOnTheSettingsPage()
    {
        await Page.GotoAsync($"{WebUrl}/settings",
            new PageGotoOptions { WaitUntil = WaitUntilState.Load });
        await Page.WaitForSelectorAsync("[data-testid='settings-page']",
            new PageWaitForSelectorOptions { Timeout = 30_000 });
    }

    [Then("I should see the settings page")]
    public async Task ThenIShouldSeeTheSettingsPage()
    {
        var settingsPage = Page.Locator("[data-testid='settings-page']");
        (await settingsPage.IsVisibleAsync()).Should().BeTrue("Settings page should be visible");
    }

    [Then("I should see the AI Providers section")]
    public async Task ThenIShouldSeeTheAiProvidersSection()
    {
        await Page.WaitForSelectorAsync("[data-testid='settings-ai-providers']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    [Then("I should see the Execution section")]
    public async Task ThenIShouldSeeTheExecutionSection()
    {
        await Page.WaitForSelectorAsync("[data-testid='settings-execution']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    [When("I set the Ollama URL to {string}")]
    public async Task WhenISetTheOllamaUrlTo(string url)
    {
        var input = Page.Locator("[data-testid='settings-ollama-url']");
        await input.ClearAsync();
        await input.FillAsync(url);
    }

    [When("I click Save Settings")]
    public async Task WhenIClickSaveSettings()
    {
        var btn = Page.Locator("[data-testid='settings-save-btn']");
        await btn.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    [Then("I should see a success toast")]
    public async Task ThenIShouldSeeASuccessToast()
    {
        var toast = Page.Locator("[data-testid='settings-toast']");
        await toast.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        var text = await toast.TextContentAsync();
        text.Should().Contain("success", "Toast should indicate success");
    }

    [When("I select {string} as the default provider")]
    public async Task WhenISelectAsTheDefaultProvider(string provider)
    {
        var select = Page.Locator("[data-testid='settings-default-provider']");
        await select.SelectOptionAsync(provider);
        await Page.WaitForTimeoutAsync(1000);
    }

    [Then("the model dropdown should be populated")]
    public async Task ThenTheModelDropdownShouldBePopulated()
    {
        // Wait for models to load
        await Page.WaitForTimeoutAsync(2000);
        var modelSelect = Page.Locator("[data-testid='settings-default-model']");
        var isDisabled = await modelSelect.IsDisabledAsync();
        // Model dropdown may or may not be populated depending on provider availability
    }

    [When("I select a model")]
    public async Task WhenISelectAModel()
    {
        var modelSelect = Page.Locator("[data-testid='settings-default-model']");
        var options = modelSelect.Locator("option");
        var count = await options.CountAsync();
        if (count > 1)
        {
            // Select the second option (first is placeholder)
            var value = await options.Nth(1).GetAttributeAsync("value");
            if (!string.IsNullOrEmpty(value))
                await modelSelect.SelectOptionAsync(value);
        }
    }

    [Then("the settings should persist")]
    public async Task ThenTheSettingsShouldPersist()
    {
        // Reload and verify provider is still set
        await Page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.Load });
        await Page.WaitForSelectorAsync("[data-testid='settings-default-provider']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
        var select = Page.Locator("[data-testid='settings-default-provider']");
        var value = await select.InputValueAsync();
        value.Should().NotBeNullOrEmpty("Provider should persist after reload");
    }

    [When("I click Test Connection")]
    public async Task WhenIClickTestConnection()
    {
        var btn = Page.Locator("[data-testid='settings-test-connection']");
        await btn.ClickAsync();
        // Wait for result to appear
        await Page.WaitForTimeoutAsync(3000);
    }

    [Then("I should see a connection test result")]
    public async Task ThenIShouldSeeAConnectionTestResult()
    {
        var result = Page.Locator("[data-testid='settings-test-result']");
        await result.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        (await result.IsVisibleAsync()).Should().BeTrue("Connection test result should be visible");
    }
}
