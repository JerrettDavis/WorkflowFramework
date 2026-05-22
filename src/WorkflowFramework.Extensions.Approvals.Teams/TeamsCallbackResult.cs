namespace WorkflowFramework.Extensions.Approvals.Teams;

/// <summary>
/// Represents the result of processing a Teams callback payload.
/// </summary>
/// <param name="StatusCode">The HTTP status code to return to the caller.</param>
/// <param name="Message">An optional human-readable message describing the outcome.</param>
public sealed record TeamsCallbackResult(int StatusCode, string? Message);
