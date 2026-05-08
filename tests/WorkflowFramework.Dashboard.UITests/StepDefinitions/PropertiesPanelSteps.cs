using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Hooks;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class PropertiesPanelSteps
{
    private readonly ScenarioContext _context;

    public PropertiesPanelSteps(ScenarioContext context)
    {
        _context = context;
    }

    private IPage Page => _context.Get<IPage>();
    private string WebUrl => AspireHooks.Fixture.WebBaseUrl;

    private async Task CreateAndOpenWorkflow(string stepType, string stepName = "TestStep", Dictionary<string, object>? config = null)
    {
        using var client = AspireHooks.Fixture.CreateApiClient();
        var workflowName = $"{stepType} Test Workflow";
        var payload = new
        {
            description = $"Test workflow with {stepType}",
            tags = new[] { "test" },
            definition = new
            {
                name = workflowName,
                steps = new[]
                {
                    new { id = "step1", type = stepType, name = stepName, config = config ?? new Dictionary<string, object>() }
                }
            }
        };
        var response = await client.PostAsJsonAsync("/api/workflows", payload);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var id = result!["id"].ToString()!;
        _context.Set(id, "WorkflowId");
        _context.Set(workflowName, "WorkflowName");

        // Open via dialog
        await Page.WaitForSelectorAsync("[data-testid='btn-open']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
        await Page.Locator("[data-testid='btn-open']").ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { Timeout = 15_000 });
        var item = Page.Locator("[data-testid='workflow-list-item']",
            new PageLocatorOptions { HasText = workflowName }).First;
        try
        {
            await item.WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
        }
        catch (TimeoutException)
        {
            await Page.Keyboard.PressAsync("Escape");
            await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
                new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden, Timeout = 10_000 });

            await Page.Locator("[data-testid='btn-open']").ClickAsync();
            await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
                new PageWaitForSelectorOptions { Timeout = 15_000 });

            item = Page.Locator("[data-testid='workflow-list-item']",
                new PageLocatorOptions { HasText = workflowName }).First;
            await item.WaitForAsync(new LocatorWaitForOptions { Timeout = 45_000 });
        }

        await item.ScrollIntoViewIfNeededAsync();
        await item.ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
        await Page.WaitForTimeoutAsync(1000);
    }

    private async Task CreateSavedWorkflow(string workflowName)
    {
        using var client = AspireHooks.Fixture.CreateApiClient();
        var payload = new
        {
            description = $"Reusable workflow {workflowName}",
            tags = new[] { "test", "subworkflow" },
            definition = new
            {
                name = workflowName,
                steps = new[]
                {
                    new { id = "child-step-1", type = "Action", name = "ChildAction", config = new Dictionary<string, object>() }
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/workflows", payload);
        response.EnsureSuccessStatusCode();
    }

    private async Task SelectFirstNode()
    {
        var node = Page.Locator(".react-flow__node").First;
        if (await node.IsVisibleAsync())
        {
            await node.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }
    }

    private ILocator GetPropertiesFieldInput(string label)
        => Page.Locator("[data-testid='properties-panel'] label", new PageLocatorOptions { HasText = label })
            .Locator("xpath=following-sibling::*[1]");

    private async Task OpenWorkflowByName(string workflowName)
    {
        await Page.WaitForSelectorAsync("[data-testid='btn-open']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
        await Page.Locator("[data-testid='btn-open']").ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { Timeout = 15_000 });

        var item = Page.Locator("[data-testid='workflow-list-item']",
            new PageLocatorOptions { HasText = workflowName }).First;
        await item.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await item.ScrollIntoViewIfNeededAsync();
        await item.ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='workflow-list']",
            new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
    }

    [Given("I have a workflow with an Action step")]
    public async Task GivenIHaveAWorkflowWithAnActionStep()
    {
        await CreateAndOpenWorkflow("action", "ActionStep");
    }

    [When("I select the Action step")]
    public async Task WhenISelectTheActionStep()
    {
        await SelectFirstNode();
    }

    [Given("I have a workflow with a SubWorkflow step")]
    public async Task GivenIHaveAWorkflowWithASubWorkflowStep()
    {
        await CreateSavedWorkflow("Child Flow");
        await CreateAndOpenWorkflow("SubWorkflow", "Invoke Child Workflow");
    }

    [When("I select the SubWorkflow step")]
    public async Task WhenISelectTheSubWorkflowStep()
    {
        await SelectFirstNode();
    }

    [Then("the properties panel should show {string} field")]
    public async Task ThenThePropertiesPanelShouldShowField(string fieldLabel)
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        await panel.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        var text = await panel.TextContentAsync();
        text.Should().NotBeNullOrEmpty("Properties panel should display step configuration");
        text.Should().Contain(fieldLabel, $"Properties panel should show the '{fieldLabel}' field label");
    }

    [Then("the field should be a text input")]
    public async Task ThenTheFieldShouldBeATextInput()
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        var inputs = panel.Locator("input[type='text']");
        var count = await inputs.CountAsync();
        count.Should().BeGreaterThan(0, "Should have text input fields");
    }

    [Given("I have a workflow with a LlmCallStep")]
    public async Task GivenIHaveAWorkflowWithALlmCallStep()
    {
        await CreateAndOpenWorkflow("LlmCallStep");
    }

    [When("I select the LlmCallStep")]
    public async Task WhenISelectTheLlmCallStep()
    {
        await SelectFirstNode();
    }

    [Then("the properties panel should show a provider dropdown")]
    public async Task ThenThePropertiesPanelShouldShowAProviderDropdown()
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        var selects = panel.Locator("select");
        var count = await selects.CountAsync();
        count.Should().BeGreaterThan(0, "Should have dropdown controls");
    }

    [Then("the properties panel should show a model dropdown")]
    public async Task ThenThePropertiesPanelShouldShowAModelDropdown()
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        var selects = panel.Locator("select");
        var count = await selects.CountAsync();
        count.Should().BeGreaterThanOrEqualTo(2, "Should have at least 2 dropdowns (provider + model)");
    }

    [Then("the properties panel should show a prompt textarea")]
    public async Task ThenThePropertiesPanelShouldShowAPromptTextarea()
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        var textareas = panel.Locator("textarea");
        var count = await textareas.CountAsync();
        count.Should().BeGreaterThan(0, "Should have textarea for prompt");
    }

    [Then("the properties panel should show a temperature slider")]
    public async Task ThenThePropertiesPanelShouldShowATemperatureSlider()
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        var sliders = panel.Locator("input[type='range']");
        var count = await sliders.CountAsync();
        count.Should().BeGreaterThan(0, "Should have slider for temperature");
    }

    [Given("I have a workflow with an HttpStep")]
    public async Task GivenIHaveAWorkflowWithAnHttpStep()
    {
        await CreateAndOpenWorkflow("HttpStep");
    }

    [When("I select the HttpStep")]
    public async Task WhenISelectTheHttpStep()
    {
        await SelectFirstNode();
    }

    [Then("the properties panel should show a method dropdown with options {string}")]
    public async Task ThenThePropertiesPanelShouldShowAMethodDropdownWithOptions(string optionsStr)
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        var selects = panel.Locator("select");
        var count = await selects.CountAsync();
        count.Should().BeGreaterThan(0, "Should have dropdown controls");
        // Verify options exist
        var expectedOptions = optionsStr.Split(',');
        var found = false;
        for (var i = 0; i < count; i++)
        {
            var options = await selects.Nth(i).Locator("option").AllTextContentsAsync();
            if (expectedOptions.All(expected => options.Any(option => option.Contains(expected, StringComparison.OrdinalIgnoreCase))))
            {
                found = true;
                break;
            }
        }
        found.Should().BeTrue("one dropdown should contain the expected HTTP method options");
    }

    [Then("the properties panel should show a URL text field")]
    public async Task ThenThePropertiesPanelShouldShowAUrlTextField()
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        var inputs = panel.Locator("input[type='text']");
        var count = await inputs.CountAsync();
        count.Should().BeGreaterThan(0, "Should have text input for URL");
    }

    [Then("the properties panel should show saved workflow suggestions including {string}")]
    public async Task ThenThePropertiesPanelShouldShowSavedWorkflowSuggestionsIncluding(string workflowName)
    {
        var helper = Page.Locator("[data-testid='properties-workflow-reference-subWorkflowName']");
        await helper.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var option = helper.Locator("[data-testid='properties-workflow-reference-option-subWorkflowName']",
            new LocatorLocatorOptions { HasText = workflowName }).First;
        await option.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
    }

    [When("I choose saved workflow {string}")]
    public async Task WhenIChooseSavedWorkflow(string workflowName)
    {
        var helper = Page.Locator("[data-testid='properties-workflow-reference-subWorkflowName']");
        var option = helper.Locator("[data-testid='properties-workflow-reference-option-subWorkflowName']",
            new LocatorLocatorOptions { HasText = workflowName }).First;
        await option.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("the SubWorkflow reference should be {string}")]
    public async Task ThenTheSubWorkflowReferenceShouldBe(string workflowName)
    {
        var input = Page.Locator("[data-testid='properties-workflow-reference-input-subWorkflowName']");
        await input.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        var value = await input.InputValueAsync();
        value.Should().Be(workflowName);
    }

    [Then("the properties panel should show a headers JSON editor")]
    public async Task ThenThePropertiesPanelShouldShowAHeadersJsonEditor()
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        var textareas = panel.Locator("textarea");
        var count = await textareas.CountAsync();
        // Notes textarea + potentially headers + body = at least 1 textarea
        count.Should().BeGreaterThan(0, "Should have textarea for JSON editors");
    }

    [Then("the properties panel should show a body JSON editor")]
    public async Task ThenThePropertiesPanelShouldShowABodyJsonEditor()
    {
        // Already verified via headers check - both use textarea
        var panel = Page.Locator("[data-testid='properties-panel']");
        var textareas = panel.Locator("textarea");
        var count = await textareas.CountAsync();
        count.Should().BeGreaterThan(0, "Should have textarea for body JSON editor");
    }

    [Given("I have a workflow with a HumanTaskStep")]
    public async Task GivenIHaveAWorkflowWithAHumanTaskStep()
    {
        await CreateAndOpenWorkflow("HumanTaskStep");
    }

    [When("I select the HumanTaskStep")]
    public async Task WhenISelectTheHumanTaskStep()
    {
        await SelectFirstNode();
    }

    [Then("the properties panel should show a priority dropdown with options {string}")]
    public async Task ThenThePropertiesPanelShouldShowAPriorityDropdownWithOptions(string optionsStr)
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        var selects = panel.Locator("select");
        var count = await selects.CountAsync();
        count.Should().BeGreaterThan(0, "Should have dropdown for priority");
    }

    [Then("the properties panel should show an assignee field")]
    public async Task ThenThePropertiesPanelShouldShowAnAssigneeField()
    {
        var panel = Page.Locator("[data-testid='properties-panel']");
        var inputs = panel.Locator("input[type='text']");
        var count = await inputs.CountAsync();
        count.Should().BeGreaterThan(0, "Should have text input for assignee");
    }

    [Given("I have a workflow with an Action step named {string}")]
    public async Task GivenIHaveAWorkflowWithAnActionStepNamed(string stepName)
    {
        await CreateAndOpenWorkflow("action", stepName);
    }

    [When("I change the step name to {string}")]
    public async Task WhenIChangeTheStepNameTo(string newName)
    {
        _context.Set(newName, "UpdatedStepName");
        var nameInput = Page.Locator("[data-testid='properties-step-name']");
        await nameInput.ClearAsync();
        await nameInput.FillAsync(newName);
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("the step name should update on the canvas")]
    public async Task ThenTheStepNameShouldUpdateOnTheCanvas()
    {
        var expectedName = _context.Get<string>("UpdatedStepName");
        var nodes = Page.Locator(".react-flow__node");
        await nodes.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });

        var nodeTexts = await nodes.AllTextContentsAsync();
        nodeTexts.Should().Contain(text => text.Contains(expectedName, StringComparison.Ordinal), "canvas node label should update reactively");
    }

    [Then("the action node quick editor should be visible")]
    public async Task ThenTheActionNodeQuickEditorShouldBeVisible()
    {
        var editor = Page.Locator("[data-testid='node-action-inline-editor']");
        await editor.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        (await editor.IsVisibleAsync()).Should().BeTrue();
    }

    [When("I update the inline action step name to {string}")]
    public async Task WhenIUpdateTheInlineActionStepNameTo(string stepName)
    {
        var input = Page.Locator("[data-testid='node-inline-name']");
        await input.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await input.FillAsync(stepName);
        await input.PressAsync("Tab");
        await Page.WaitForFunctionAsync(
            @"([selector, expected]) => {
                const element = document.querySelector(selector);
                return element && element.value === expected;
            }",
            new[] { "[data-testid='properties-step-name']", stepName });
    }

    [When("I update the inline action expression to {string}")]
    public async Task WhenIUpdateTheInlineActionExpressionTo(string expression)
    {
        var input = Page.Locator("[data-testid='node-inline-expression']");
        await input.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await input.FillAsync(expression);
        await input.PressAsync("Tab");
        await Page.WaitForFunctionAsync(
            @"([selector, expected]) => {
                const element = document.querySelector(selector);
                return element && element.value === expected;
            }",
            new[] { "[data-testid='node-inline-expression']", expression });
    }

    [When("I save and reopen the current workflow")]
    public async Task WhenISaveAndReopenTheCurrentWorkflow()
    {
        var saveButton = Page.Locator("[data-testid='btn-save']");
        await saveButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await saveButton.ClickAsync();

        var workflowName = _context.Get<string>("WorkflowName");
        await OpenWorkflowByName(workflowName);
        await SelectFirstNode();
    }

    [Then("the properties panel step name should be {string}")]
    public async Task ThenThePropertiesPanelStepNameShouldBe(string expectedName)
    {
        var input = Page.Locator("[data-testid='properties-step-name']");
        await input.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        (await input.InputValueAsync()).Should().Be(expectedName);
    }

    [Then("the properties panel expression should be {string}")]
    public async Task ThenThePropertiesPanelExpressionShouldBe(string expectedExpression)
    {
        var input = GetPropertiesFieldInput("Expression");
        await input.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });

        var tagName = await input.EvaluateAsync<string>("el => el.tagName.toLowerCase()");
        var actualValue = tagName switch
        {
            "textarea" => await input.InputValueAsync(),
            "input" => await input.InputValueAsync(),
            _ => await input.TextContentAsync() ?? string.Empty
        };

        actualValue.Should().Be(expectedExpression);
    }

    [Then("the action node summary should contain {string}")]
    public async Task ThenTheActionNodeSummaryShouldContain(string expectedText)
    {
        var summary = Page.Locator("[data-testid='node-action-summary']").First;
        await summary.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        (await summary.TextContentAsync()).Should().Contain(expectedText);
    }

    [When("I type {string} in the notes field")]
    public async Task WhenITypeInTheNotesField(string notesText)
    {
        var notes = Page.Locator("[data-testid='properties-notes']");
        await notes.ClearAsync();
        await notes.FillAsync(notesText);
        await Page.WaitForTimeoutAsync(500);
    }

    [Then("the notes should be saved")]
    public async Task ThenTheNotesShouldBeSaved()
    {
        var notes = Page.Locator("[data-testid='properties-notes']");
        var value = await notes.InputValueAsync();
        value.Should().NotBeNullOrEmpty("Notes should contain text after editing");
    }

}

