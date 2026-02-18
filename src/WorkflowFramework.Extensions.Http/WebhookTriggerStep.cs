using System.Collections.Concurrent;

namespace WorkflowFramework.Extensions.Http;

/// <summary>
/// A workflow step that pauses execution until a webhook callback is received.
/// </summary>
public sealed class WebhookTriggerStep : IStep
{
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<WebhookPayload>> _pendingWebhooks = new();

    private readonly WebhookTriggerOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="WebhookTriggerStep"/>.
    /// </summary>
    public WebhookTriggerStep(WebhookTriggerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string Name => _options.Name ?? "WebhookTrigger";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var callbackId = _options.CallbackIdFactory?.Invoke(context) ?? context.CorrelationId;
        var tcs = new TaskCompletionSource<WebhookPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingWebhooks[callbackId] = tcs;

        context.Properties[$"{Name}.CallbackId"] = callbackId;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            cts.CancelAfter(_options.Timeout);
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                var payload = await tcs.Task.ConfigureAwait(false);
                context.Properties[$"{Name}.Received"] = true;
                context.Properties[$"{Name}.Body"] = payload.Body;
                foreach (var kv in payload.Headers)
                    context.Properties[$"{Name}.Header.{kv.Key}"] = kv.Value;
            }
        }
        catch (TaskCanceledException)
        {
            context.Properties[$"{Name}.Received"] = false;
        }
        finally
        {
            _pendingWebhooks.TryRemove(callbackId, out _);
        }
    }

    /// <summary>
    /// Delivers a webhook callback to a waiting workflow step.
    /// </summary>
    /// <param name="callbackId">The callback ID.</param>
    /// <param name="payload">The webhook payload.</param>
    /// <returns>True if a waiting step was found and notified.</returns>
    public static bool DeliverWebhook(string callbackId, WebhookPayload payload)
    {
        if (_pendingWebhooks.TryRemove(callbackId, out var tcs))
        {
            return tcs.TrySetResult(payload);
        }
        return false;
    }
}

/// <summary>
/// Options for configuring a webhook trigger step.
/// </summary>
public sealed class WebhookTriggerOptions
{
    /// <summary>Gets or sets the step name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the timeout for waiting.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Gets or sets a factory for generating callback IDs.</summary>
    public Func<IWorkflowContext, string>? CallbackIdFactory { get; set; }
}

/// <summary>
/// Represents data received via a webhook callback.
/// </summary>
public sealed class WebhookPayload
{
    /// <summary>Gets or sets the body content.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets or sets the headers.</summary>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
}
