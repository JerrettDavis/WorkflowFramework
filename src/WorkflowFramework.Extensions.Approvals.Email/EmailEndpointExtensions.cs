using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WorkflowFramework.Extensions.Approvals.Email;

/// <summary>
/// Extension methods for mapping the email approval callback endpoint into an ASP.NET Core
/// minimal API application.
/// </summary>
public static class EmailEndpointExtensions
{
    /// <summary>
    /// Registers a <c>GET</c> endpoint that handles email approval callback links.
    /// Approvers click the approve or reject URL in their email; the token is validated,
    /// and the vote is recorded via <see cref="PersistentApprovalService"/>.
    /// </summary>
    /// <param name="app">The endpoint route builder to register the route on.</param>
    /// <param name="path">
    /// The route path override. When <see langword="null"/>, the value from
    /// <see cref="EmailApprovalOptions.CallbackPath"/> is used.
    /// </param>
    /// <returns>The same <paramref name="app"/> for chaining.</returns>
    public static IEndpointRouteBuilder MapEmailApprovalCallback(
        this IEndpointRouteBuilder app,
        string? path = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Resolve path lazily so we don't force options evaluation at registration time.
        app.MapGet(path ?? GetCallbackPath(app), async (
            HttpContext context,
            [Microsoft.AspNetCore.Mvc.FromQuery(Name = "t")] string? token,
            PersistentApprovalService persistent,
            ApprovalTokenService tokenService,
            CancellationToken cancellationToken) =>
        {
            return await EmailApprovalCallbackHandler.HandleAsync(token, persistent, tokenService, cancellationToken)
                .ConfigureAwait(false);
        });

        return app;
    }

    private static string GetCallbackPath(IEndpointRouteBuilder app)
    {
        // Best-effort: try to resolve from DI. Fall back to the well-known default.
        try
        {
            var options = app.ServiceProvider.GetService<IOptions<EmailApprovalOptions>>();
            if (options?.Value?.CallbackPath is { Length: > 0 } p)
                return p;
        }
        catch
        {
            // Ignore — use default.
        }

        return "/approvals/email/respond";
    }
}
