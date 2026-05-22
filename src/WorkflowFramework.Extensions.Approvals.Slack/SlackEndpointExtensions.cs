using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WorkflowFramework.Extensions.Approvals.Slack;

/// <summary>
/// Provides ASP.NET Core minimal API extension methods for registering the Slack
/// interactivity callback endpoint.
/// </summary>
public static class SlackEndpointExtensions
{
    /// <summary>
    /// Maps the Slack approval interactivity endpoint at the configured callback path.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <param name="path">
    /// Optional override for the callback path. When <see langword="null"/>, the path from
    /// <see cref="SlackApprovalOptions.CallbackPath"/> is used.
    /// </param>
    /// <returns>The same <see cref="IEndpointRouteBuilder"/> for chaining.</returns>
    public static IEndpointRouteBuilder MapSlackApprovalInteractivity(
        this IEndpointRouteBuilder app,
        string? path = null)
    {
        if (app is null) throw new ArgumentNullException(nameof(app));

        // Resolve the effective path
        string effectivePath;
        if (path is not null)
        {
            effectivePath = path;
        }
        else
        {
            // Attempt to read from options if available; fall back to default
            var options = app.ServiceProvider?.GetService<IOptions<SlackApprovalOptions>>();
            effectivePath = options?.Value.CallbackPath ?? "/approvals/slack/interact";
        }

        app.MapPost(effectivePath, async (HttpContext context) =>
        {
            var handler = context.RequestServices.GetRequiredService<SlackInteractivityHandler>();

            // Read headers
            var timestamp = context.Request.Headers["X-Slack-Request-Timestamp"].FirstOrDefault() ?? string.Empty;
            var signature = context.Request.Headers["X-Slack-Signature"].FirstOrDefault() ?? string.Empty;

            // Read raw body
            string rawBody;
            using (var reader = new StreamReader(context.Request.Body))
            {
                rawBody = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            var result = await handler.HandleAsync(timestamp, signature, rawBody, context.RequestAborted)
                .ConfigureAwait(false);

            context.Response.StatusCode = result.StatusCode;

            if (!string.IsNullOrEmpty(result.Message))
            {
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync(result.Message, context.RequestAborted).ConfigureAwait(false);
            }
        });

        return app;
    }
}
