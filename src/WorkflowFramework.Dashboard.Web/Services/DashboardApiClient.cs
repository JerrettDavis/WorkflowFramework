using System.Net.Http.Headers;
using System.Net.Http.Json;
using WorkflowFramework.Dashboard.Web.Models;

namespace WorkflowFramework.Dashboard.Web.Services;

/// <summary>
/// Typed HTTP client for the Dashboard API.
/// </summary>
public sealed class DashboardApiClient(HttpClient http)
{
    // Auth
    public void SetAuthToken(string? token)
    {
        http.DefaultRequestHeaders.Authorization = token is not null
            ? new AuthenticationHeaderValue("Bearer", token)
            : null;
    }

    public async Task<AuthResult?> LoginAsync(string username, string password)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/login", new { username, password });
        return await resp.Content.ReadFromJsonAsync<AuthResult>();
    }

    public async Task<AuthResult?> RegisterAsync(string username, string email, string password, string? displayName)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/register", new { username, email, password, displayName });
        return await resp.Content.ReadFromJsonAsync<AuthResult>();
    }

    public async Task<AuthResult?> RefreshTokenAsync(string refreshToken)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        return await resp.Content.ReadFromJsonAsync<AuthResult>();
    }

    public async Task<UserProfile?> GetCurrentUserAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<UserProfile>("/api/auth/me", ct);

    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/change-password", new { currentPassword, newPassword });
        return resp.IsSuccessStatusCode;
    }

    // API Keys
    public async Task<List<ApiKeyInfo>> GetApiKeysAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<ApiKeyInfo>>("/api/auth/api-keys", ct) ?? [];

    public async Task<CreateApiKeyResult?> CreateApiKeyAsync(string name, List<string> scopes, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/api-keys", new CreateApiKeyRequest { Name = name, Scopes = scopes }, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CreateApiKeyResult>(ct);
    }

    public async Task RevokeApiKeyAsync(string id, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"/api/auth/api-keys/{id}", ct);
        resp.EnsureSuccessStatusCode();
    }

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

    // Import/Export
    public async Task<WorkflowExportDto?> ExportWorkflowAsync(string id, CancellationToken ct = default)
        => await http.GetFromJsonAsync<WorkflowExportDto>($"/api/workflows/{id}/export", ct);

    public async Task<string> ExportWorkflowYamlAsync(string id, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/api/workflows/{id}/export?format=yaml", ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public async Task<SavedWorkflowDefinition?> ImportWorkflowAsync(string json, CancellationToken ct = default)
    {
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var resp = await http.PostAsync("/api/workflows/import", content, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SavedWorkflowDefinition>(ct);
    }

    public async Task<SavedWorkflowDefinition?> ImportWorkflowYamlAsync(string yaml, CancellationToken ct = default)
    {
        var content = new StringContent(yaml, System.Text.Encoding.UTF8, "text/yaml");
        var resp = await http.PostAsync("/api/workflows/import?format=yaml", content, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SavedWorkflowDefinition>(ct);
    }

    // Webhooks
    public async Task<WebhookTriggerResponseDto?> TriggerWebhookAsync(string workflowId, string? payload = null, bool async_ = false, CancellationToken ct = default)
    {
        var url = $"/api/webhooks/{workflowId}/trigger" + (async_ ? "?async" : "");
        var content = payload is not null ? new StringContent(payload, System.Text.Encoding.UTF8, "application/json") : null;
        var resp = await http.PostAsync(url, content, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<WebhookTriggerResponseDto>(ct);
    }

    // Scheduling
    public async Task SetScheduleAsync(string workflowId, string cronExpression, bool enabled = true, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync($"/api/workflows/{workflowId}/schedule", new { cronExpression, enabled }, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task RemoveScheduleAsync(string workflowId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"/api/workflows/{workflowId}/schedule", ct);
        resp.EnsureSuccessStatusCode();
    }

    // Triggers
    public async Task<List<TriggerTypeInfoDto>> GetTriggerTypesAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<TriggerTypeInfoDto>>("/api/triggers/types", ct) ?? [];

    public async Task<List<TriggerDefinitionDto>> GetWorkflowTriggersAsync(string workflowId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<TriggerDefinitionDto>>($"/api/workflows/{workflowId}/triggers", ct) ?? [];

    public async Task SaveWorkflowTriggersAsync(string workflowId, List<TriggerDefinitionDto> triggers, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync($"/api/workflows/{workflowId}/triggers", new { Triggers = triggers }, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<RunSummary?> TestTriggerAsync(string workflowId, string triggerId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"/api/workflows/{workflowId}/triggers/{triggerId}/test", null, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RunSummary>(ct);
    }
}
