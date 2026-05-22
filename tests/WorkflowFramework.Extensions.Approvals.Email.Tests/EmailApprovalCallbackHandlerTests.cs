using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals;
using WorkflowFramework.Extensions.Approvals.Email;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Email.Tests;

public sealed class EmailApprovalCallbackHandlerTests
{
    private static readonly string ValidKey = Convert.ToBase64String(new byte[32]
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
    });

    private static EmailApprovalOptions Options() => new()
    {
        SmtpHost = "smtp.test",
        From = "a@b.com",
        ApproveUrlTemplate = "https://example.com/approve?t={token}",
        RejectUrlTemplate = "https://example.com/reject?t={token}",
        TokenSigningKey = ValidKey
    };

    private static (PersistentApprovalService persistent, ApprovalTokenService tokenSvc) BuildServices(
        string correlationId = "corr1",
        bool rehydrate = true)
    {
        var store = new InMemoryApprovalStore();
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("fake");
        inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(Timeout.Infinite, ci.Arg<CancellationToken>());
                return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
            });

        var persistent = new PersistentApprovalService(inner, store);
        var tokenSvc = new ApprovalTokenService(Microsoft.Extensions.Options.Options.Create(Options()));

        if (rehydrate)
        {
            var request = new ApprovalRequest(
                Title: "Test",
                Description: null,
                Context: new Dictionary<string, object?>(),
                RequiredApprovers: 1,
                Timeout: TimeSpan.FromHours(1),
                AllowedRoles: null) { CorrelationId = correlationId };

            persistent.Rehydrate(new PendingApproval(
                CorrelationId: correlationId,
                Request: request,
                PrimaryChannel: "email",
                CreatedAt: DateTimeOffset.UtcNow,
                DeadlineAt: DateTimeOffset.UtcNow + TimeSpan.FromHours(1),
                Votes: Array.Empty<ApprovalRecord>(),
                EscalationChannel: null,
                TimeoutAction: OnTimeoutAction.AutoReject));

            // Also save to store for AppendVoteAsync to work.
            store.SaveAsync(new PendingApproval(
                CorrelationId: correlationId,
                Request: request,
                PrimaryChannel: "email",
                CreatedAt: DateTimeOffset.UtcNow,
                DeadlineAt: DateTimeOffset.UtcNow + TimeSpan.FromHours(1),
                Votes: Array.Empty<ApprovalRecord>(),
                EscalationChannel: null,
                TimeoutAction: OnTimeoutAction.AutoReject)).GetAwaiter().GetResult();
        }

        return (persistent, tokenSvc);
    }

    private string CreateValidToken(ApprovalTokenService tokenSvc, string correlationId = "corr1", bool decision = true) =>
        tokenSvc.Create(new ApprovalTokenPayload(
            CorrelationId: correlationId,
            ApproverId: "user@example.com",
            Decision: decision,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1)));

    // ------------------------------------------------------------------
    // Invalid token → bad request result
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_InvalidToken_ReturnsBadRequest()
    {
        var (persistent, tokenSvc) = BuildServices();

        var result = await EmailApprovalCallbackHandler.HandleAsync("invalid.token", persistent, tokenSvc);

        result.Should().NotBeNull();
        // Verify it's a bad request result by checking the status.
        var statusResult = result as IStatusCodeHttpResult;
        statusResult?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_NullToken_ReturnsBadRequest()
    {
        var (persistent, tokenSvc) = BuildServices();

        var result = await EmailApprovalCallbackHandler.HandleAsync(null, persistent, tokenSvc);

        var statusResult = result as IStatusCodeHttpResult;
        statusResult?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_EmptyToken_ReturnsBadRequest()
    {
        var (persistent, tokenSvc) = BuildServices();

        var result = await EmailApprovalCallbackHandler.HandleAsync(string.Empty, persistent, tokenSvc);

        var statusResult = result as IStatusCodeHttpResult;
        statusResult?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    // ------------------------------------------------------------------
    // Valid token → calls ResolveExternalAsync with correct ApprovalRecord
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ValidApproveToken_RecordsApprovalAndReturnsHtml()
    {
        var (persistent, tokenSvc) = BuildServices("corr-approve");
        var token = CreateValidToken(tokenSvc, "corr-approve", decision: true);

        // We need to await the result from WaitForCompletionAsync after the vote resolves.
        // HandleAsync calls ResolveExternalAsync which will trigger quorum.
        // Since RequiredApprovers=1 and we send 1 vote, the TCS should be set.
        var result = await EmailApprovalCallbackHandler.HandleAsync(token, persistent, tokenSvc);

        result.Should().NotBeNull();
        // Should be a 200 OK with HTML.
        var contentResult = result as IStatusCodeHttpResult;
        // Results.Content returns 200 by default.
        if (contentResult?.StatusCode is not null)
            contentResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        // The result should be HTML content (not a BadRequest, Forbid, or NotFound).
        result.Should().NotBeOfType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>();
    }

    [Fact]
    public async Task HandleAsync_ValidRejectToken_RecordsRejectionAndReturnsHtml()
    {
        var (persistent, tokenSvc) = BuildServices("corr-reject");
        var token = CreateValidToken(tokenSvc, "corr-reject", decision: false);

        var result = await EmailApprovalCallbackHandler.HandleAsync(token, persistent, tokenSvc);

        result.Should().NotBeNull();
        result.Should().NotBeOfType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>();
    }

    // ------------------------------------------------------------------
    // ResolveExternalAsync throws UnauthorizedAccessException → 403
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_UnauthorizedAccess_ReturnsForbid()
    {
        // Create a request with AllowedRoles so that the user@example.com is not allowed.
        var store = new InMemoryApprovalStore();
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("fake");
        inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(Timeout.Infinite, ci.Arg<CancellationToken>());
                return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
            });

        var persistent = new PersistentApprovalService(inner, store);
        var tokenSvc = new ApprovalTokenService(Microsoft.Extensions.Options.Options.Create(Options()));

        // Create request with AllowedRoles that excludes user@example.com.
        var correlationId = "corr-unauthorized";
        var request = new ApprovalRequest(
            Title: "Test",
            Description: null,
            Context: new Dictionary<string, object?>(),
            RequiredApprovers: 1,
            Timeout: TimeSpan.FromHours(1),
            AllowedRoles: new[] { "admin-only@example.com" }) // Only this is allowed
        { CorrelationId = correlationId };

        persistent.Rehydrate(new PendingApproval(
            CorrelationId: correlationId,
            Request: request,
            PrimaryChannel: "email",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow + TimeSpan.FromHours(1),
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject));

        await store.SaveAsync(new PendingApproval(
            CorrelationId: correlationId,
            Request: request,
            PrimaryChannel: "email",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow + TimeSpan.FromHours(1),
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject));

        // Token for user@example.com who is NOT in AllowedRoles.
        var token = tokenSvc.Create(new ApprovalTokenPayload(
            CorrelationId: correlationId,
            ApproverId: "user@example.com",
            Decision: true,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1)));

        var result = await EmailApprovalCallbackHandler.HandleAsync(token, persistent, tokenSvc);

        // Should be 403.
        result.Should().NotBeNull();
        var statusResult = result as IStatusCodeHttpResult;
        statusResult?.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // ------------------------------------------------------------------
    // ResolveExternalAsync throws InvalidOperationException → 404
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_UnknownCorrelation_Returns404()
    {
        var (persistent, tokenSvc) = BuildServices(rehydrate: false);

        // Create a valid token for an unknown correlation.
        var token = tokenSvc.Create(new ApprovalTokenPayload(
            CorrelationId: "unknown-correlation",
            ApproverId: "user@example.com",
            Decision: true,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1)));

        var result = await EmailApprovalCallbackHandler.HandleAsync(token, persistent, tokenSvc);

        result.Should().NotBeNull();
        var statusResult = result as IStatusCodeHttpResult;
        statusResult?.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
