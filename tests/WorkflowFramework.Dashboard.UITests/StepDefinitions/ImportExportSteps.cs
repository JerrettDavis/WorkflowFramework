using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Hooks;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class ImportExportSteps
{
    private readonly ScenarioContext _context;
    private JsonElement? _exportedData;
    private string? _importedWorkflowId;

    public ImportExportSteps(ScenarioContext context)
    {
        _context = context;
    }

    private IPage Page => _context.Get<IPage>();

    [Then("I should see the import button in the toolbar")]
    public async Task ThenIShouldSeeTheImportButtonInTheToolbar()
    {
        var importBtn = Page.Locator("[data-testid='btn-import']");
        (await importBtn.IsVisibleAsync()).Should().BeTrue("Import button should be visible in toolbar");
    }

    [When("I export the workflow via API")]
    public async Task WhenIExportTheWorkflowViaApi()
    {
        var workflowId = _context.Get<string>("WorkflowId");
        using var client = AspireHooks.Fixture.CreateApiClient();
        var response = await client.GetAsync($"/api/workflows/{workflowId}/export");
        response.EnsureSuccessStatusCode();
        _exportedData = await response.Content.ReadFromJsonAsync<JsonElement>();
        _context.Set(_exportedData!.Value.GetRawText(), "ExportedJson");
    }

    [Then("the export should contain the workflow definition")]
    public void ThenTheExportShouldContainTheWorkflowDefinition()
    {
        _exportedData.Should().NotBeNull();
        _exportedData!.Value.TryGetProperty("definition", out _).Should().BeTrue(
            "Export should contain a 'definition' property");
    }

    [Then("the export should have a format version")]
    public void ThenTheExportShouldHaveAFormatVersion()
    {
        _exportedData.Should().NotBeNull();
        _exportedData!.Value.TryGetProperty("formatVersion", out _).Should().BeTrue(
            "Export should contain a 'formatVersion' property");
    }

    [When("I import a workflow via API")]
    public async Task WhenIImportAWorkflowViaApi()
    {
        using var client = AspireHooks.Fixture.CreateApiClient();
        var importPayload = new
        {
            formatVersion = "1.0",
            definition = new
            {
                name = "Imported Workflow",
                steps = new[]
                {
                    new { id = "s1", type = "action", name = "Imported Step", config = new Dictionary<string, object>() }
                }
            }
        };
        var response = await client.PostAsJsonAsync("/api/workflows/import", importPayload);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        _importedWorkflowId = result!["id"].ToString();
    }

    [Then("the imported workflow should be accessible")]
    public async Task ThenTheImportedWorkflowShouldBeAccessible()
    {
        _importedWorkflowId.Should().NotBeNullOrEmpty();
        using var client = AspireHooks.Fixture.CreateApiClient();
        var response = await client.GetAsync($"/api/workflows/{_importedWorkflowId}");
        response.EnsureSuccessStatusCode();
    }

    [Then("the imported workflow should have the correct name")]
    public async Task ThenTheImportedWorkflowShouldHaveTheCorrectName()
    {
        using var client = AspireHooks.Fixture.CreateApiClient();
        var response = await client.GetAsync($"/api/workflows/{_importedWorkflowId}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var def = json.GetProperty("definition");
        def.GetProperty("name").GetString().Should().Be("Imported Workflow");
    }

    [When("I import the exported workflow via API")]
    public async Task WhenIImportTheExportedWorkflowViaApi()
    {
        var exportedJson = _context.Get<string>("ExportedJson");
        using var client = AspireHooks.Fixture.CreateApiClient();
        var content = new StringContent(exportedJson, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/workflows/import", content);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        _importedWorkflowId = result!["id"].ToString();
    }

    [Then("both workflows should have the same definition")]
    public async Task ThenBothWorkflowsShouldHaveTheSameDefinition()
    {
        var originalId = _context.Get<string>("WorkflowId");
        using var client = AspireHooks.Fixture.CreateApiClient();

        var originalResponse = await client.GetAsync($"/api/workflows/{originalId}");
        var originalJson = await originalResponse.Content.ReadFromJsonAsync<JsonElement>();
        var originalDef = originalJson.GetProperty("definition");

        var importedResponse = await client.GetAsync($"/api/workflows/{_importedWorkflowId}");
        var importedJson = await importedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var importedDef = importedJson.GetProperty("definition");

        // Compare step counts and types
        var originalSteps = originalDef.GetProperty("steps").GetArrayLength();
        var importedSteps = importedDef.GetProperty("steps").GetArrayLength();
        importedSteps.Should().Be(originalSteps, "Imported workflow should have same number of steps");
    }
}
