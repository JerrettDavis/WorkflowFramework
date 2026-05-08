using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using WorkflowFramework.Dashboard.Web.Models;

namespace WorkflowFramework.Dashboard.Web.Services;

/// <summary>
/// Typed HTTP client for the Dashboard API.
/// </summary>
public sealed class DashboardApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions ValidationJsonOptions = new(JsonSerializerDefaults.Web);

    public Uri? BaseAddress => http.BaseAddress;

    public string? GetExecutionHubUrl()
        => GetExecutionHubUrls().FirstOrDefault();

    public IReadOnlyList<string> GetExecutionHubUrls()
    {
        if (http.BaseAddress is null)
            return [];

        var scheme = http.BaseAddress.Scheme;
        if (string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            return [new Uri(http.BaseAddress, "/hubs/execution").ToString()];
        }

        var tokens = scheme.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0 || string.IsNullOrWhiteSpace(http.BaseAddress.Authority))
            return [];

        var urls = new List<string>();
        foreach (var token in tokens)
        {
            if (!token.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                !token.Equals("https", StringComparison.OrdinalIgnoreCase))
                continue;

            var baseUrl = $"{token.ToLowerInvariant()}://{http.BaseAddress.Authority}";
            var hubUrl = new Uri(new Uri(baseUrl), "/hubs/execution").ToString();
            if (!urls.Contains(hubUrl, StringComparer.OrdinalIgnoreCase))
                urls.Add(hubUrl);
        }

        return urls;
    }

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
        return await ReadValidationResultAsync(resp, ct);
    }

    public async Task<ValidationResultDto?> ValidateWorkflowDraftAsync(string id, WorkflowDefinitionDto definition, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync($"/api/workflows/{id}/validate-draft", definition, ct);
        resp.EnsureSuccessStatusCode();
        return await ReadValidationResultAsync(resp, ct);
    }

    public async Task<ValidationResultDto?> ValidateDefinitionAsync(WorkflowDefinitionDto definition, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/workflows/validate", definition, ct);
        resp.EnsureSuccessStatusCode();
        return await ReadValidationResultAsync(resp, ct);
    }

    // Steps
    public async Task<List<StepTypeInfo>> GetStepTypesAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<StepTypeInfo>>("/api/steps", ct) ?? [];

    public async Task<StepTypeInfo?> GetStepTypeAsync(string type, CancellationToken ct = default)
        => await http.GetFromJsonAsync<StepTypeInfo>($"/api/steps/{Uri.EscapeDataString(type)}", ct);

    // Runs
    public async Task<RunSummary?> RunWorkflowAsync(string id, StartRunRequestDto? request = null, CancellationToken ct = default)
    {
        HttpResponseMessage resp;
        if (request is null)
            resp = await http.PostAsync($"/api/workflows/{id}/run", null, ct);
        else
            resp = await http.PostAsJsonAsync($"/api/workflows/{id}/run", request, ct);

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
    {
        var resp = await http.GetAsync($"/api/runs/{runId}", ct);
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null;

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RunSummary>(ct);
    }

    public async Task<bool> CancelRunAsync(string runId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"/api/runs/{runId}", ct);
        return resp.IsSuccessStatusCode;
    }

    // Settings
    public async Task<DashboardSettingsDto?> GetSettingsAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<DashboardSettingsDto>("/api/settings", ct);

    public async Task<DashboardSettingsDto?> UpdateSettingsAsync(UpdateDashboardSettingsRequest settings, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync("/api/settings", settings, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<DashboardSettingsDto>(ct);
    }

    public async Task<List<string>> GetProviderModelsAsync(string provider, string? ollamaUrl = null, CancellationToken ct = default)
    {
        var url = $"/api/providers/{Uri.EscapeDataString(provider)}/models";
        if (!string.IsNullOrWhiteSpace(ollamaUrl))
            url += $"?ollamaUrl={Uri.EscapeDataString(ollamaUrl)}";

        return await http.GetFromJsonAsync<List<string>>(url, ct) ?? [];
    }

    public async Task<OllamaTestResult?> TestOllamaConnectionAsync(string? ollamaUrl = null, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/settings/test-ollama", new TestOllamaConnectionRequest
        {
            OllamaUrl = ollamaUrl
        }, ct);
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

    // History Graph
    public async Task<List<HistoryNodeSummary>> GetHistoryNodesAsync(string? query = null, int limit = 50, CancellationToken ct = default)
    {
        var url = $"/api/history/nodes?limit={limit}";
        if (!string.IsNullOrWhiteSpace(query))
            url += $"&query={Uri.EscapeDataString(query)}";
        return await http.GetFromJsonAsync<List<HistoryNodeSummary>>(url, ct) ?? [];
    }

    public async Task<List<HistoryEdgeSummary>> GetHistoryEdgesAsync(string? workflow = null, long minWeight = 1, CancellationToken ct = default)
    {
        var url = "/api/history/edges";
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(workflow)) q.Add($"workflow={Uri.EscapeDataString(workflow)}");
        if (q.Count > 0) url += "?" + string.Join("&", q);
        var edges = await http.GetFromJsonAsync<List<HistoryEdgeSummary>>(url, ct) ?? [];
        return minWeight > 1 ? edges.Where(e => e.Weight >= minWeight).ToList() : edges;
    }

    public async Task<string?> GetHistoryMermaidAsync(int maxNodes = 50, long minEdgeWeight = 1, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/api/history/mermaid?maxNodes={maxNodes}&minEdgeWeight={minEdgeWeight}", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public async Task<string?> GetHistoryMermaidSubgraphAsync(string fingerprint, int maxDepth = 5, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/api/history/mermaid/{Uri.EscapeDataString(fingerprint)}?maxDepth={maxDepth}", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync(ct);
    }

    // Voice
    public async Task<List<VoiceSubmissionDto>> GetVoiceSubmissionsAsync(int? limit = null, CancellationToken ct = default)
    {
        var url = "/api/voice/submissions";
        if (limit.HasValue)
            url += $"?limit={limit.Value}";
        return await http.GetFromJsonAsync<List<VoiceSubmissionDto>>(url, ct) ?? [];
    }

    public async Task<VoiceSubmissionDto?> CreateVoiceSubmissionAsync(VoiceSubmissionRequestDto request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/voice/submissions", request, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<VoiceSubmissionDto>(ct);
    }

    private static async Task<ValidationResultDto?> ReadValidationResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(content))
            return new ValidationResultDto();

        try
        {
            return JsonSerializer.Deserialize<ValidationResultDto>(content, ValidationJsonOptions);
        }
        catch (JsonException)
        {
            return ParseValidationResultFallback(content);
        }
    }

    private static ValidationResultDto ParseValidationResultFallback(string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        var result = new ValidationResultDto();
        if (root.ValueKind != JsonValueKind.Object)
            return result;

        if (TryGetPropertyIgnoreCase(root, "errors", out var errorsEl) && errorsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in errorsEl.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var severity = "Error";
                if (TryGetPropertyIgnoreCase(item, "severity", out var severityEl))
                    severity = ParseSeverity(severityEl);

                string? stepName = null;
                if (TryGetPropertyIgnoreCase(item, "stepName", out var stepEl) && stepEl.ValueKind == JsonValueKind.String)
                    stepName = stepEl.GetString();

                var message = "";
                if (TryGetPropertyIgnoreCase(item, "message", out var messageEl))
                {
                    message = messageEl.ValueKind == JsonValueKind.String
                        ? messageEl.GetString() ?? ""
                        : messageEl.GetRawText();
                }

                result.Errors.Add(new ValidationErrorDto
                {
                    Severity = severity,
                    StepName = stepName,
                    Message = message
                });
            }
        }

        var defaultErrors = result.Errors.Count(e => e.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));
        var defaultWarnings = result.Errors.Count(e => e.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase));

        result.ErrorCount = TryGetInt(root, "errorCount", defaultErrors);
        result.WarningCount = TryGetInt(root, "warningCount", defaultWarnings);
        result.IsValid = TryGetBool(root, "isValid", result.ErrorCount == 0);
        return result;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (property.NameEquals(name) || property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string ParseSeverity(JsonElement severityEl)
    {
        return severityEl.ValueKind switch
        {
            JsonValueKind.String => NormalizeSeverity(severityEl.GetString()),
            JsonValueKind.Number when severityEl.TryGetInt32(out var number) => number switch
            {
                0 => "Info",
                1 => "Warning",
                2 => "Error",
                _ => "Error"
            },
            JsonValueKind.Object when TryGetPropertyIgnoreCase(severityEl, "name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                => NormalizeSeverity(nameEl.GetString()),
            JsonValueKind.Object when TryGetPropertyIgnoreCase(severityEl, "value", out var valueEl)
                => ParseSeverity(valueEl),
            _ => "Error"
        };
    }

    private static string NormalizeSeverity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Error";

        if (value.Equals("info", StringComparison.OrdinalIgnoreCase) || value == "0")
            return "Info";

        if (value.Equals("warning", StringComparison.OrdinalIgnoreCase) || value == "1")
            return "Warning";

        return "Error";
    }

    private static int TryGetInt(JsonElement root, string name, int fallback)
    {
        if (TryGetPropertyIgnoreCase(root, name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var value))
            return value;

        return fallback;
    }

    private static bool TryGetBool(JsonElement root, string name, bool fallback)
    {
        if (TryGetPropertyIgnoreCase(root, name, out var el))
        {
            if (el.ValueKind == JsonValueKind.True) return true;
            if (el.ValueKind == JsonValueKind.False) return false;
        }

        return fallback;
    }
}
