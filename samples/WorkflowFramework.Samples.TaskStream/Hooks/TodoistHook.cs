using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Hooks;

/// <summary>
/// Configuration for the Todoist integration.
/// </summary>
public sealed class TodoistOptions
{
    /// <summary>Gets or sets the Todoist API key. If null/empty, the hook is a no-op.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the target project ID.</summary>
    public string? ProjectId { get; set; }
}

/// <summary>
/// Creates/updates/completes tasks in Todoist via the REST API v1.
/// Only active when an API key is configured.
/// </summary>
public sealed class TodoistHook : ITodoHook
{
    private const string BaseUrl = "https://api.todoist.com/api/v1";
    private readonly HttpClient _http;
    private readonly TodoistOptions _options;

    /// <summary>Initializes a new instance.</summary>
    public TodoistHook(HttpClient http, IOptions<TodoistOptions> options)
    {
        _http = http;
        _options = options.Value;

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    private bool IsEnabled => !string.IsNullOrEmpty(_options.ApiKey);

    /// <inheritdoc />
    public async Task OnTaskCreatedAsync(TodoItem item, CancellationToken ct = default)
    {
        if (!IsEnabled) return;

        try
        {
            var body = new Dictionary<string, object?>
            {
                ["content"] = item.Title,
                ["description"] = item.Description,
                ["priority"] = item.Priority
            };
            if (!string.IsNullOrEmpty(_options.ProjectId))
                body["project_id"] = _options.ProjectId;

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"{BaseUrl}/tasks", content, ct);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("id", out var idProp))
            {
                item.Enrichments["todoist_id"] = idProp.GetString() ?? "";
            }
        }
        catch
        {
            // Best-effort Todoist sync
        }
    }

    /// <inheritdoc />
    public Task OnTaskUpdatedAsync(TodoItem item, CancellationToken ct = default) =>
        Task.CompletedTask; // Todoist update not implemented in this sample

    /// <inheritdoc />
    public async Task OnTaskCompletedAsync(TodoItem item, CancellationToken ct = default)
    {
        if (!IsEnabled) return;
        if (!item.Enrichments.TryGetValue("todoist_id", out var todoistId)) return;

        try
        {
            var response = await _http.PostAsync($"{BaseUrl}/tasks/{todoistId}/close", null, ct);
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            // Best-effort Todoist sync
        }
    }
}
