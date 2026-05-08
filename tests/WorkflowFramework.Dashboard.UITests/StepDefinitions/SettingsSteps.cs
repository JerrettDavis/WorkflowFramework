using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;
using System.Text.Json;
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
        // Wait for Blazor circuit to connect (prerendered HTML has the elements but they're not interactive)
        await Page.WaitForFunctionAsync(
            "() => window.Blazor !== undefined",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("I should see the settings page")]
    public async Task ThenIShouldSeeTheSettingsPage()
    {
        var settingsPage = Page.Locator("[data-testid='settings-page']");
        await settingsPage.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
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
        _context["OllamaUrl"] = url;
        var input = Page.Locator("[data-testid='settings-ollama-url']");
        await input.ClearAsync();
        await input.FillAsync(url);
    }

    [When("I click Save Settings")]
    public async Task WhenIClickSaveSettings()
    {
        var btn = Page.Locator("[data-testid='settings-save-btn']");
        await btn.ClickAsync();
        var toast = Page.Locator("[data-testid='settings-toast']");
        await toast.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
    }

    [Then("I should see a success toast")]
    public async Task ThenIShouldSeeASuccessToast()
    {
        var toast = Page.Locator("[data-testid='settings-toast']");
        await toast.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        var text = await toast.TextContentAsync();
        text.Should().Contain("success", "Toast should indicate success");
    }

    [When("I select {string} as the default provider")]
    public async Task WhenISelectAsTheDefaultProvider(string provider)
    {
        _context["SelectedProvider"] = provider;
        var select = Page.Locator("[data-testid='settings-default-provider']");
        await select.SelectOptionAsync(provider);
        await select.EvaluateAsync(
            "element => { element.dispatchEvent(new Event('input', { bubbles: true })); element.dispatchEvent(new Event('change', { bubbles: true })); }");
        await Page.WaitForFunctionAsync(
            "expectedProvider => { const element = document.querySelector(\"[data-testid='settings-default-provider']\"); return element && element.value === expectedProvider; }",
            provider,
            new PageWaitForFunctionOptions { Timeout = 5_000 });
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("the model dropdown should be populated")]
    public async Task ThenTheModelDropdownShouldBePopulated()
    {
        var modelSelect = Page.Locator("[data-testid='settings-default-model']");
        await modelSelect.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        var options = modelSelect.Locator("option");
        var provider = _context.TryGetValue<string>("SelectedProvider", out var selectedProvider)
            ? selectedProvider
            : null;
        var installedOllamaModels = await GetInstalledOllamaModelsAsync(provider);

        IReadOnlyList<string> values = [];
        for (var i = 0; i < 30; i++)
        {
            var count = await options.CountAsync();
            values = await ReadOptionValuesAsync(options, count);
            if (count > 1 &&
                !(await modelSelect.IsDisabledAsync()) &&
                ModelsMatchProvider(values, provider, installedOllamaModels))
            {
                return;
            }

            await Page.WaitForTimeoutAsync(500);
        }

        values.Count.Should().BeGreaterThan(1, "provider models should be available");
        (await modelSelect.IsDisabledAsync()).Should().BeFalse();
        ModelsMatchProvider(values, provider, installedOllamaModels)
            .Should().BeTrue("model options should match the selected provider");
    }

    [When("I select a model")]
    public async Task WhenISelectAModel()
    {
        var modelSelect = Page.Locator("[data-testid='settings-default-model']");
        var options = modelSelect.Locator("option");
        var count = await options.CountAsync();
        if (count <= 1)
            return;

        var values = await ReadOptionValuesAsync(options, count);
        var provider = _context.TryGetValue<string>("SelectedProvider", out var selectedProvider)
            ? selectedProvider
            : null;

        string? selectedValue = null;
        if (string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase))
        {
            var installedOllamaModels = await GetInstalledOllamaModelsAsync(provider);
            selectedValue = values
                .Skip(1)
                .FirstOrDefault(value => installedOllamaModels.Contains(value, StringComparer.OrdinalIgnoreCase));
        }

        selectedValue ??= values.Skip(1).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
        if (string.IsNullOrEmpty(selectedValue))
            return;

        _context["SelectedModel"] = selectedValue;
        await modelSelect.SelectOptionAsync(selectedValue);
        await modelSelect.EvaluateAsync(
            "element => { element.dispatchEvent(new Event('input', { bubbles: true })); element.dispatchEvent(new Event('change', { bubbles: true })); }");
        await Page.WaitForFunctionAsync(
            "expectedModel => { const element = document.querySelector(\"[data-testid='settings-default-model']\"); return element && element.value === expectedModel; }",
            selectedValue,
            new PageWaitForFunctionOptions { Timeout = 5_000 });
    }

    [Then("the settings should persist")]
    public async Task ThenTheSettingsShouldPersist()
    {
        // Reload and verify provider is still set
        await WhenIReloadTheSettingsPage();
        var select = Page.Locator("[data-testid='settings-default-provider']");
        var value = await select.InputValueAsync();
        value.Should().NotBeNullOrEmpty("Provider should persist after reload");
    }

    [Then("{string} should be selected as the default provider")]
    public async Task ThenShouldBeSelectedAsTheDefaultProvider(string provider)
    {
        var select = Page.Locator("[data-testid='settings-default-provider']");
        await select.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        (await select.InputValueAsync()).Should().Be(provider);
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
        var text = await result.TextContentAsync();
        text.Should().NotBeNullOrWhiteSpace();
        text.Should().MatchRegex("(Connected to Ollama|returned|failed|error)");
    }

    [When("I enter {string} as the OpenAI API key")]
    public async Task WhenIEnterAsTheOpenAiApiKey(string apiKey)
    {
        var input = Page.Locator("[data-testid='settings-openai-api-key']");
        await input.FillAsync(apiKey);
    }

    [When("I reload the settings page")]
    public async Task WhenIReloadTheSettingsPage()
    {
        await Page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.Load });
        await Page.WaitForSelectorAsync("[data-testid='settings-page']",
            new PageWaitForSelectorOptions { Timeout = 30_000 });
        await Page.WaitForFunctionAsync(
            "() => window.Blazor !== undefined",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
    }

    [Then("the OpenAI provider should show as configured")]
    public async Task ThenTheOpenAiProviderShouldShowAsConfigured()
    {
        var badge = Page.Locator("[data-testid='settings-openai-status']");
        await badge.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        (await badge.TextContentAsync()).Should().Contain("Saved");
    }

    [Then("the {string} provider should show as not configured")]
    public async Task ThenTheProviderShouldShowAsNotConfigured(string provider)
    {
        var badge = Page.Locator(GetProviderStatusSelector(provider));
        await badge.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        (await badge.TextContentAsync()).Should().Contain("Not configured");
    }

    [Then("the OpenAI API key field should be empty")]
    public async Task ThenTheOpenAiApiKeyFieldShouldBeEmpty()
    {
        var input = Page.Locator("[data-testid='settings-openai-api-key']");
        await input.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        (await input.InputValueAsync()).Should().BeEmpty();
    }

    [Then("the {string} API key field should be empty")]
    public async Task ThenTheApiKeyFieldShouldBeEmpty(string provider)
    {
        var input = Page.Locator(GetProviderApiKeySelector(provider));
        await input.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        (await input.InputValueAsync()).Should().BeEmpty();
    }

    private static string GetProviderStatusSelector(string provider)
        => provider.ToLowerInvariant() switch
        {
            "openai" => "[data-testid='settings-openai-status']",
            "anthropic" => "[data-testid='settings-anthropic-status']",
            "huggingface" => "[data-testid='settings-huggingface-status']",
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider")
        };

    private static string GetProviderApiKeySelector(string provider)
        => provider.ToLowerInvariant() switch
        {
            "openai" => "[data-testid='settings-openai-api-key']",
            "anthropic" => "[data-testid='settings-anthropic-api-key']",
            "huggingface" => "[data-testid='settings-huggingface-api-key']",
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider")
        };

    private static async Task<IReadOnlyList<string>> ReadOptionValuesAsync(ILocator options, int count)
    {
        var values = new List<string>(count);
        for (var i = 0; i < count; i++)
            values.Add(await options.Nth(i).GetAttributeAsync("value") ?? string.Empty);

        return values;
    }

    private async Task<HashSet<string>> GetInstalledOllamaModelsAsync(string? provider)
    {
        if (!string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase))
            return [];

        var ollamaUrl = _context.TryGetValue<string>("OllamaUrl", out var url)
            ? url
            : "http://localhost:11434";

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(ollamaUrl), Timeout = TimeSpan.FromSeconds(5) };
            using var response = await client.GetAsync("/api/tags");
            if (!response.IsSuccessStatusCode)
                return [];

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            if (!document.RootElement.TryGetProperty("models", out var modelsElement) || modelsElement.ValueKind != JsonValueKind.Array)
                return [];

            return modelsElement.EnumerateArray()
                .Select(static item => item.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
        }
        catch
        {
            return [];
        }
    }

    private static bool ModelsMatchProvider(IReadOnlyList<string> values, string? provider, IReadOnlySet<string> installedOllamaModels)
    {
        var actualValues = values.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList();
        if (actualValues.Count == 0 || string.IsNullOrWhiteSpace(provider))
            return actualValues.Count > 0;

        return provider.ToLowerInvariant() switch
        {
            "ollama" => installedOllamaModels.Count > 0
                ? actualValues.Any(value => installedOllamaModels.Contains(value))
                : actualValues.Any(static value =>
                    value.Contains(':', StringComparison.Ordinal) ||
                    value is "llama3.1" or "llama3.2" or "mistral" or "codellama" or "phi3" or "gemma2" or "qwen2.5"),
            "openai" => actualValues.Any(static value => value.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase) || value.StartsWith("o1", StringComparison.OrdinalIgnoreCase)),
            "anthropic" => actualValues.Any(static value => value.StartsWith("claude-", StringComparison.OrdinalIgnoreCase)),
            "huggingface" => actualValues.Any(static value => value.Contains('/', StringComparison.Ordinal)),
            _ => true
        };
    }
}


