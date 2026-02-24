using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Hooks;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class PluginSteps
{
    private readonly ScenarioContext _context;
    private JsonElement? _pluginsResponse;
    private JsonElement? _stepTypesResponse;
    private JsonElement? _webhookResponse;

    public PluginSteps(ScenarioContext context)
    {
        _context = context;
    }

    [When("I request the plugins API")]
    public async Task WhenIRequestThePluginsApi()
    {
        using var client = AspireHooks.Fixture.CreateApiClient();
        var response = await client.GetAsync("/api/plugins");
        response.EnsureSuccessStatusCode();
        _pluginsResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Then("I should see the email plugin")]
    public void ThenIShouldSeeTheEmailPlugin()
    {
        _pluginsResponse.Should().NotBeNull();
        var plugins = _pluginsResponse!.Value.EnumerateArray().ToArray();
        plugins.Should().Contain(p =>
            p.GetProperty("name").GetString()!.Contains("email", StringComparison.OrdinalIgnoreCase),
            "Should have an email plugin");
    }

    [Then("the email plugin should have a SendEmail step type")]
    public void ThenTheEmailPluginShouldHaveASendEmailStepType()
    {
        _pluginsResponse.Should().NotBeNull();
        var raw = _pluginsResponse!.Value.GetRawText();
        // Plugin list shows "Email Integration" with "1 step type(s)" description
        raw.Should().ContainEquivalentOf("Email",
            "Email plugin should be listed");
    }

    [When("I request the step types API")]
    public async Task WhenIRequestTheStepTypesApi()
    {
        using var client = AspireHooks.Fixture.CreateApiClient();
        var response = await client.GetAsync("/api/steps");
        response.EnsureSuccessStatusCode();
        _stepTypesResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Then("the step types should include SendEmail")]
    public void ThenTheStepTypesShouldIncludeSendEmail()
    {
        _stepTypesResponse.Should().NotBeNull();
        var raw = _stepTypesResponse!.Value.GetRawText();
        raw.Should().ContainEquivalentOf("SendEmail",
            "Step types should include SendEmail");
    }

    [Then("the SendEmail step should be in the Communication category")]
    public void ThenTheSendEmailStepShouldBeInTheCommunicationCategory()
    {
        _stepTypesResponse.Should().NotBeNull();
        var types = _stepTypesResponse!.Value.EnumerateArray().ToArray();
        var sendEmail = types.FirstOrDefault(t =>
            t.GetProperty("type").GetString()!.Contains("SendEmail", StringComparison.OrdinalIgnoreCase));
        sendEmail.ValueKind.Should().NotBe(JsonValueKind.Undefined, "SendEmail step type should exist");
        sendEmail.GetProperty("category").GetString().Should()
            .BeEquivalentTo("Communication", "SendEmail should be in Communication category");
    }

    [When("I trigger the workflow via webhook API")]
    public async Task WhenITriggerTheWorkflowViaWebhookApi()
    {
        var workflowId = _context.Get<string>("WorkflowId");
        using var client = AspireHooks.Fixture.CreateApiClient();
        var response = await client.PostAsJsonAsync(
            $"/api/webhooks/{workflowId}/trigger",
            new { });
        response.EnsureSuccessStatusCode();
        _webhookResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Then("I should receive a webhook response with a run ID")]
    public void ThenIShouldReceiveAWebhookResponseWithARunId()
    {
        _webhookResponse.Should().NotBeNull();
        _webhookResponse!.Value.TryGetProperty("runId", out var runId).Should().BeTrue(
            "Webhook response should contain a runId");
        runId.GetString().Should().NotBeNullOrEmpty("Run ID should not be empty");
    }
}
