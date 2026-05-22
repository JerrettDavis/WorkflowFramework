using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace WorkflowFramework.Extensions.Approvals.Teams;

/// <summary>
/// Extension methods for mapping the Teams approval callback endpoint in a Minimal API application.
/// </summary>
public static class TeamsEndpointExtensions
{
    /// <summary>
    /// Maps the Teams Adaptive Card <c>Action.Submit</c> callback endpoint.
    /// </summary>
    /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="path">
    /// Override the route path.  When <see langword="null"/> the path from
    /// <see cref="TeamsApprovalOptions.CallbackPath"/> is used.
    /// </param>
    /// <returns>The same <see cref="IEndpointRouteBuilder"/> for chaining.</returns>
    public static IEndpointRouteBuilder MapTeamsApprovalCallback(
        this IEndpointRouteBuilder app,
        string? path = null)
    {
        if (app is null) throw new ArgumentNullException(nameof(app));

        var options = app.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<TeamsApprovalOptions>>()
            .Value;

        var routePath = path ?? options.CallbackPath;

        app.MapPost(routePath, async (HttpContext context) =>
        {
            JsonNode? payload;
            try
            {
                payload = await JsonNode.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted)
                    .ConfigureAwait(false);
            }
            catch
            {
                return Results.BadRequest("Request body is not valid JSON.");
            }

            var handler = context.RequestServices.GetRequiredService<TeamsCallbackHandler>();
            var result = await handler.HandleAsync(payload, context.RequestAborted).ConfigureAwait(false);

            return result.StatusCode switch
            {
                200 => Results.Ok(result.Message),
                400 => Results.BadRequest(result.Message),
                401 => Results.Unauthorized(),
                403 => Results.Forbid(),
                404 => Results.NotFound(result.Message),
                _ => Results.StatusCode(result.StatusCode),
            };
        });

        return app;
    }
}
