using System.Text.Json;
using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Hooks;

/// <summary>
/// Posts todo item events to a configurable webhook URL.
/// </summary>
public sealed class WebhookNotifyHook : ITodoHook
{
    private readonly HttpClient _http;
    private readonly string? _webhookUrl;

    /// <summary>Initializes a new instance with the given HTTP client and webhook URL.</summary>
    public WebhookNotifyHook(HttpClient http, string? webhookUrl)
    {
        _http = http;
        _webhookUrl = webhookUrl;
    }

    /// <inheritdoc />
    public Task OnTaskCreatedAsync(TodoItem item, CancellationToken ct = default) =>
        PostAsync("created", item, ct);

    /// <inheritdoc />
    public Task OnTaskUpdatedAsync(TodoItem item, CancellationToken ct = default) =>
        PostAsync("updated", item, ct);

    /// <inheritdoc />
    public Task OnTaskCompletedAsync(TodoItem item, CancellationToken ct = default) =>
        PostAsync("completed", item, ct);

    private async Task PostAsync(string eventType, TodoItem item, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_webhookUrl)) return;

        try
        {
            var payload = JsonSerializer.Serialize(new { @event = eventType, task = item });
            using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            await _http.PostAsync(_webhookUrl, content, ct);
        }
        catch
        {
            // Best-effort webhook notification
        }
    }
}
