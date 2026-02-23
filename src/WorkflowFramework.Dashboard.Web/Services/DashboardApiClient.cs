using System.Net.Http.Json;
using WorkflowFramework.Dashboard.Web.Models;

namespace WorkflowFramework.Dashboard.Web.Services;

/// <summary>
/// Typed HTTP client for the Dashboard API.
/// </summary>
public sealed class DashboardApiClient(HttpClient http)
{
    // Templates
    public async Task<List<WorkflowTemplateSummary>> GetTemplatesAsync(string? category = null, CancellationToken ct = default)
    {
        var url = "/api/templates";
        if (!string.IsNullOrEmpty(category)) url += $"?category={Uri.EscapeDataString(category)}";
        return await http.GetFromJsonAsync<List<WorkflowTemplateSummary>>(url, ct) ?? [];
    }

    public async Task<WorkflowTemplate?> GetTemplateAsync(string id, CancellationToken ct = default)
        => await http.GetFromJsonAsync<WorkflowTemplate>($"/api/templates/{id}", ct);

    public async Task<List<string>> GetTemplateCategoriesAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<string>>("/api/templates/categories", ct) ?? [];

    public async Task<SavedWorkflowDefinition?> UseTemplateAsync(string id, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"/api/templates/{id}/use", null, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SavedWorkflowDefinition>(ct);
    }

    // Workflows
    public async Task<List<WorkflowListItem>> GetWorkflowsAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<WorkflowListItem>>("/api/workflows", ct) ?? [];

    public async Task<SavedWorkflowDefinition?> GetWorkflowAsync(string id, CancellationToken ct = default)
        => await http.GetFromJsonAsync<SavedWorkflowDefinition>($"/api/workflows/{id}", ct);

    public async Task<SavedWorkflowDefinition?> SaveWorkflowAsync(CreateWorkflowRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/workflows", request, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SavedWorkflowDefinition>(ct);
    }

    public async Task<SavedWorkflowDefinition?> UpdateWorkflowAsync(string id, CreateWorkflowRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync($"/api/workflows/{id}", request, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SavedWorkflowDefinition>(ct);
    }

    public async Task<bool> DeleteWorkflowAsync(string id, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"/api/workflows/{id}", ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<SavedWorkflowDefinition?> DuplicateWorkflowAsync(string id, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"/api/workflows/{id}/duplicate", null, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SavedWorkflowDefinition>(ct);
    }

    // Validation
    public async Task<ValidationResultDto?> ValidateWorkflowAsync(string id, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"/api/workflows/{id}/validate", null, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ValidationResultDto>(ct);
    }

    public async Task<ValidationResultDto?> ValidateDefinitionAsync(WorkflowDefinitionDto definition, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/workflows/validate", definition, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ValidationResultDto>(ct);
    }

    // Steps
    public async Task<List<StepTypeInfo>> GetStepTypesAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<StepTypeInfo>>("/api/steps", ct) ?? [];

    public async Task<StepTypeInfo?> GetStepTypeAsync(string type, CancellationToken ct = default)
        => await http.GetFromJsonAsync<StepTypeInfo>($"/api/steps/{Uri.EscapeDataString(type)}", ct);

    // Runs
    public async Task<RunSummary?> RunWorkflowAsync(string id, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"/api/workflows/{id}/run", null, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RunSummary>(ct);
    }

    public async Task<List<RunSummary>> GetRunsAsync(int? limit = null, CancellationToken ct = default)
    {
        var url = "/api/runs";
        if (limit.HasValue) url += $"?limit={limit.Value}";
        return await http.GetFromJsonAsync<List<RunSummary>>(url, ct) ?? [];
    }

    public async Task<RunSummary?> GetRunAsync(string runId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<RunSummary>($"/api/runs/{runId}", ct);

    public async Task<bool> CancelRunAsync(string runId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"/api/runs/{runId}", ct);
        return resp.IsSuccessStatusCode;
    }

    // Settings
    public async Task<DashboardSettingsDto?> GetSettingsAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<DashboardSettingsDto>("/api/settings", ct);

    public async Task<DashboardSettingsDto?> UpdateSettingsAsync(DashboardSettingsDto settings, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync("/api/settings", settings, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<DashboardSettingsDto>(ct);
    }

    public async Task<List<string>> GetProviderModelsAsync(string provider, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<string>>($"/api/providers/{Uri.EscapeDataString(provider)}/models", ct) ?? [];

    public async Task<OllamaTestResult?> TestOllamaConnectionAsync(CancellationToken ct = default)
    {
        var resp = await http.PostAsync("/api/settings/test-ollama", null, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<OllamaTestResult>(ct);
    }
}
