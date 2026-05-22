using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals.Slack;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Slack.Tests;

public sealed class SlackInteractivityHandlerTests
{
    private const string TestSecret = "test-signing-secret-abc";
    private const string WrongSecret = "wrong-secret-xyz";

    private static SlackApprovalOptions MakeOptions(string secret = TestSecret) =>
        new()
        {
            BotToken = "xoxb-test",
            ChannelId = "C123",
            SigningSecret = secret,
            RequestSignatureMaxAgeSeconds = 300
        };

    /// <summary>Computes a valid Slack signature using the current time.</summary>
    private static (string timestamp, string signature) ComputeSignature(string rawBody, string secret = TestSecret)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var sigBase = $"v0:{ts}:{rawBody}";
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var msgBytes = Encoding.UTF8.GetBytes(sigBase);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(msgBytes);
        var sig = "v0=" + Convert.ToHexString(hash).ToLowerInvariant();
        return (ts, sig);
    }

    private static string BuildPayload(string actionId, string userId = "U1", string username = "alice")
    {
        var json = $"{{\"type\":\"block_actions\",\"actions\":[{{\"action_id\":\"{actionId}\",\"value\":\"abc123\"}}],\"user\":{{\"id\":\"{userId}\",\"username\":\"{username}\"}}}}";
        return "payload=" + Uri.EscapeDataString(json);
    }

    private static (SlackInteractivityHandler handler, IApprovalStore store, PersistentApprovalService persistent)
        MakeHandlerWithSecret(string secret = TestSecret)
    {
        var validator = new SlackSignatureValidator(Options.Create(MakeOptions(secret)));
        var store = Substitute.For<IApprovalStore>();
        var innerChannel = Substitute.For<IApprovalChannel>();
        innerChannel.Name.Returns("test");
        var persistent = new PersistentApprovalService(innerChannel, store, NullLogger<PersistentApprovalService>.Instance);
        var handler = new SlackInteractivityHandler(validator, persistent, NullLogger<SlackInteractivityHandler>.Instance);
        return (handler, store, persistent);
    }

    private static PendingApproval MakePending(string correlationId, IReadOnlyList<string>? allowedRoles = null)
    {
        var builder = new ApprovalRequestBuilder()
            .WithTitle("Test Approval")
            .WithTimeout(TimeSpan.FromHours(1));

        if (allowedRoles is not null)
            builder = builder.AllowedFor(allowedRoles.ToArray());

        var request = builder.Build() with { CorrelationId = correlationId };

        return new PendingApproval(
            CorrelationId: correlationId,
            Request: request,
            PrimaryChannel: "slack",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow.AddHours(1),
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject);
    }

    [Fact]
    public async Task HandleAsync_InvalidSignature_Returns401()
    {
        // Validator uses TestSecret but we sign with WrongSecret → invalid
        var validator = new SlackSignatureValidator(Options.Create(MakeOptions(TestSecret)));
        var store = Substitute.For<IApprovalStore>();
        var innerChannel = Substitute.For<IApprovalChannel>();
        innerChannel.Name.Returns("test");
        var persistent = new PersistentApprovalService(innerChannel, store);
        var handler = new SlackInteractivityHandler(validator, persistent);

        var body = BuildPayload("approve:abc123");
        var (ts, sig) = ComputeSignature(body, WrongSecret); // wrong secret

        var result = await handler.HandleAsync(ts, sig, body, CancellationToken.None);

        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task HandleAsync_ValidApproveAction_RecordsApprovedTrueAndReturns200()
    {
        var (handler, store, persistent) = MakeHandlerWithSecret();

        var pending = MakePending("abc123");
        store.AppendVoteAsync("abc123", Arg.Any<ApprovalRecord>(), Arg.Any<CancellationToken>())
            .Returns(pending with
            {
                Votes = new[] { new ApprovalRecord("U1", "alice", true, null, DateTimeOffset.UtcNow, "slack") }
            });

        persistent.Rehydrate(pending);

        var body = BuildPayload("approve:abc123", userId: "U1", username: "alice");
        var (ts, sig) = ComputeSignature(body);

        var result = await handler.HandleAsync(ts, sig, body, CancellationToken.None);

        result.StatusCode.Should().Be(200);
        await store.Received(1).AppendVoteAsync(
            "abc123",
            Arg.Is<ApprovalRecord>(r => r.Approved == true && r.ApproverId == "U1" && r.ApproverDisplayName == "alice"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ValidRejectAction_RecordsApprovedFalseAndReturns200()
    {
        var (handler, store, persistent) = MakeHandlerWithSecret();

        var pending = MakePending("abc123");
        store.AppendVoteAsync("abc123", Arg.Any<ApprovalRecord>(), Arg.Any<CancellationToken>())
            .Returns(pending with
            {
                Votes = new[] { new ApprovalRecord("U2", "bob", false, null, DateTimeOffset.UtcNow, "slack") }
            });

        persistent.Rehydrate(pending);

        var body = BuildPayload("reject:abc123", userId: "U2", username: "bob");
        var (ts, sig) = ComputeSignature(body);

        var result = await handler.HandleAsync(ts, sig, body, CancellationToken.None);

        result.StatusCode.Should().Be(200);
        await store.Received(1).AppendVoteAsync(
            "abc123",
            Arg.Is<ApprovalRecord>(r => r.Approved == false && r.ApproverId == "U2"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ResolveExternalThrowsUnauthorized_Returns403()
    {
        var (handler, store, persistent) = MakeHandlerWithSecret();

        // Pending approval restricted to role "admin"; voter "U999" is not in that role
        var pending = MakePending("abc123", allowedRoles: new[] { "admin" });
        store.AppendVoteAsync("abc123", Arg.Any<ApprovalRecord>(), Arg.Any<CancellationToken>())
            .Returns(pending with
            {
                Votes = new[] { new ApprovalRecord("U999", "notadmin", true, null, DateTimeOffset.UtcNow, "slack") }
            });

        persistent.Rehydrate(pending);

        var body = BuildPayload("approve:abc123", userId: "U999", username: "notadmin");
        var (ts, sig) = ComputeSignature(body);

        var result = await handler.HandleAsync(ts, sig, body, CancellationToken.None);

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task HandleAsync_ResolveExternalThrowsInvalidOperation_Returns404()
    {
        var (handler, _, _) = MakeHandlerWithSecret();

        // No Rehydrate → no semaphore for "nonexistent" → InvalidOperationException
        var body = BuildPayload("approve:nonexistent");
        var (ts, sig) = ComputeSignature(body);

        var result = await handler.HandleAsync(ts, sig, body, CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task HandleAsync_MalformedPayloadJson_Returns400()
    {
        var (handler, _, _) = MakeHandlerWithSecret();
        var rawBody = "payload=" + Uri.EscapeDataString("{ not valid json ~~~ }");
        var (ts, sig) = ComputeSignature(rawBody);

        var result = await handler.HandleAsync(ts, sig, rawBody, CancellationToken.None);

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task HandleAsync_MissingActionsArray_Returns400()
    {
        var (handler, _, _) = MakeHandlerWithSecret();
        var json = """{"type":"block_actions","user":{"id":"U1","username":"alice"}}""";
        var rawBody = "payload=" + Uri.EscapeDataString(json);
        var (ts, sig) = ComputeSignature(rawBody);

        var result = await handler.HandleAsync(ts, sig, rawBody, CancellationToken.None);

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task HandleAsync_ActionIdWithNoColon_Returns400()
    {
        var (handler, _, _) = MakeHandlerWithSecret();
        var body = BuildPayload("approvewithoutcolon");
        var (ts, sig) = ComputeSignature(body);

        var result = await handler.HandleAsync(ts, sig, body, CancellationToken.None);

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task HandleAsync_UnknownActionPrefix_Returns400()
    {
        var (handler, _, _) = MakeHandlerWithSecret();
        var body = BuildPayload("unknown:abc123");
        var (ts, sig) = ComputeSignature(body);

        var result = await handler.HandleAsync(ts, sig, body, CancellationToken.None);

        result.StatusCode.Should().Be(400);
    }
}
