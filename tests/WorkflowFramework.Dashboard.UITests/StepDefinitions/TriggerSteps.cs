using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Hooks;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class TriggerSteps
{
    private readonly ScenarioContext _context;
    private JsonElement[]? _triggerTypes;

    public TriggerSteps(ScenarioContext context)
    {
        _context = context;
    }

    private IPage Page => _context.Get<IPage>();
    private string WebUrl => AspireHooks.Fixture.WebBaseUrl;

    [Given("a workflow exists")]
    public async Task GivenAWorkflowExists()
    {
        using var client = AspireHooks.Fixture.CreateApiClient();
        var payload = new
        {
            description = "Test workflow for triggers",
            tags = new[] { "test" },
            definition = new
            {
                name = "Trigger Test Workflow",
                steps = new[]
                {
                    new { id = "step1", type = "action", name = "Step 1", config = new Dictionary<string, object>() }
                }
            }
        };
        var response = await client.PostAsJsonAsync("/api/workflows", payload);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var id = result!["id"].ToString()!;
        _context.Set(id, "WorkflowId");
        _context.Set("Trigger Test Workflow", "WorkflowName");
    }

    [When("I open the workflow")]
    public async Task WhenIOpenTheWorkflow()
    {
        var name = _context.Get<string>("WorkflowName");
        await Page.WaitForSelectorAsync("[data-testid='btn-open']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
        await Page.Locator("[data-testid='btn-open']").ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { Timeout = 5_000 });

        var item = Page.Locator("[data-testid='workflow-list-item']",
            new PageLocatorOptions { HasText = name }).First;
        await item.ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
        await Page.WaitForTimeoutAsync(1000);
    }

    [When("I click the triggers tab")]
    public async Task WhenIClickTheTriggersTab()
    {
        var tab = Page.Locator("[data-testid='tab-triggers']");
        await tab.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await tab.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("I should see the trigger panel")]
    public async Task ThenIShouldSeeTheTriggerPanel()
    {
        var panel = Page.Locator("[data-testid='trigger-panel']");
        (await panel.IsVisibleAsync()).Should().BeTrue("Trigger panel should be visible");
    }

    [Then("the trigger panel should show an empty state")]
    public async Task ThenTheTriggerPanelShouldShowAnEmptyState()
    {
        var panel = Page.Locator("[data-testid='trigger-panel']");
        var items = panel.Locator("[data-testid='trigger-item']");
        var count = await items.CountAsync();
        count.Should().Be(0, "Trigger panel should have no triggers initially");
    }

    [When("I click the add trigger button")]
    public async Task WhenIClickTheAddTriggerButton()
    {
        var btn = Page.Locator("[data-testid='add-trigger-btn']");
        await btn.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await btn.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("I should see the trigger type selector")]
    public async Task ThenIShouldSeeTheTriggerTypeSelector()
    {
        var selector = Page.Locator("[data-testid='trigger-type-select']");
        (await selector.IsVisibleAsync()).Should().BeTrue("Trigger type selector should be visible");
    }

    [Then("the selector should contain schedule trigger")]
    public async Task ThenTheSelectorShouldContainScheduleTrigger()
    {
        await VerifyTriggerTypeOption("schedule");
    }

    [Then("the selector should contain webhook trigger")]
    public async Task ThenTheSelectorShouldContainWebhookTrigger()
    {
        await VerifyTriggerTypeOption("webhook");
    }

    [Then("the selector should contain file watcher trigger")]
    public async Task ThenTheSelectorShouldContainFileWatcherTrigger()
    {
        await VerifyTriggerTypeOption("file_watcher");
    }

    [Then("the selector should contain audio input trigger")]
    public async Task ThenTheSelectorShouldContainAudioInputTrigger()
    {
        await VerifyTriggerTypeOption("audio_input");
    }

    [Then("the selector should contain message queue trigger")]
    public async Task ThenTheSelectorShouldContainMessageQueueTrigger()
    {
        await VerifyTriggerTypeOption("message_queue");
    }

    [Then("the selector should contain manual trigger")]
    public async Task ThenTheSelectorShouldContainManualTrigger()
    {
        await VerifyTriggerTypeOption("manual");
    }

    private async Task VerifyTriggerTypeOption(string triggerType)
    {
        var selector = Page.Locator("[data-testid='trigger-type-select']");
        var html = await selector.InnerHTMLAsync();
        html.Should().ContainEquivalentOf(triggerType,
            $"Trigger type selector should contain '{triggerType}' option");
    }

    [When("I select trigger type {string}")]
    public async Task WhenISelectTriggerType(string triggerType)
    {
        var selector = Page.Locator("[data-testid='trigger-type-select']");
        await selector.SelectOptionAsync(new SelectOptionValue { Label = triggerType });
        await Page.WaitForTimeoutAsync(300);
    }

    [When("I confirm adding the trigger")]
    public async Task WhenIConfirmAddingTheTrigger()
    {
        var confirmBtn = Page.Locator("[data-testid='confirm-add-trigger']");
        await confirmBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("I should see a trigger item in the panel")]
    public async Task ThenIShouldSeeATriggerItemInThePanel()
    {
        var panel = Page.Locator("[data-testid='trigger-panel']");
        var items = panel.Locator("[data-testid='trigger-item']");
        var count = await items.CountAsync();
        count.Should().BeGreaterThan(0, "Should have at least one trigger item");
    }

    [When("I click remove on the trigger")]
    public async Task WhenIClickRemoveOnTheTrigger()
    {
        var removeBtn = Page.Locator("[data-testid='remove-trigger']").First;
        await removeBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
    }

    [When("I request the trigger types API")]
    public async Task WhenIRequestTheTriggerTypesApi()
    {
        using var client = AspireHooks.Fixture.CreateApiClient();
        var response = await client.GetAsync("/api/triggers/types");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        _triggerTypes = json.EnumerateArray().ToArray();
    }

    [Then("I should receive {int} trigger types")]
    public void ThenIShouldReceiveTriggerTypes(int expectedCount)
    {
        _triggerTypes.Should().NotBeNull();
        _triggerTypes!.Length.Should().Be(expectedCount,
            $"Should have {expectedCount} trigger types");
    }

    [Then("each trigger type should have a config schema")]
    public void ThenEachTriggerTypeShouldHaveAConfigSchema()
    {
        _triggerTypes.Should().NotBeNull();
        foreach (var tt in _triggerTypes!)
        {
            tt.TryGetProperty("configSchema", out _).Should().BeTrue(
                "Each trigger type should have a configSchema property");
        }
    }
}
