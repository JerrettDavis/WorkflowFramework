using Microsoft.AspNetCore.Http;

namespace WorkflowFramework.Extensions.Approvals.Email;

/// <summary>
/// Pure handler logic for the email approval callback endpoint. Extracted from
/// <see cref="EmailEndpointExtensions"/> so that the handler can be unit-tested without
/// standing up an ASP.NET Core test server.
/// </summary>
public static class EmailApprovalCallbackHandler
{
    /// <summary>
    /// Processes an approval callback token and records the vote.
    /// </summary>
    /// <param name="token">The raw token string from the query string parameter <c>t</c>.</param>
    /// <param name="persistent">The persistent approval service that tracks in-flight approvals.</param>
    /// <param name="tokenService">The token service used to validate and decode the token.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>
    /// An <see cref="IResult"/> representing the HTTP response:
    /// <list type="bullet">
    ///   <item>400 — token is missing, malformed, expired, or has an invalid signature.</item>
    ///   <item>403 — the approver is not permitted to vote on this request.</item>
    ///   <item>404 or 409 — the correlation is unknown or has already completed.</item>
    ///   <item>200 — the vote was recorded; returns a thank-you HTML page.</item>
    /// </list>
    /// </returns>
    public static async Task<IResult> HandleAsync(
        string? token,
        PersistentApprovalService persistent,
        ApprovalTokenService tokenService,
        CancellationToken cancellationToken = default)
    {
        if (!tokenService.TryParse(token, out var payload))
            return Results.BadRequest("Invalid or expired approval token.");

        var record = new ApprovalRecord(
            ApproverId: payload.ApproverId,
            ApproverDisplayName: payload.ApproverDisplayName,
            Approved: payload.Decision,
            Comment: null,
            Timestamp: DateTimeOffset.UtcNow,
            Channel: "email");

        try
        {
            await persistent.ResolveExternalAsync(payload.CorrelationId, record, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException)
        {
            // Correlation unknown or already complete.
            return Results.NotFound("The approval request was not found or has already been resolved.");
        }

        var decision = payload.Decision ? "Approved" : "Rejected";
        var html = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><title>Response Recorded</title></head>
            <body>
            <h2>Thank you.</h2>
            <p>Your response (<strong>{decision}</strong>) has been recorded.</p>
            </body>
            </html>
            """;

        return Results.Content(html, "text/html");
    }
}
