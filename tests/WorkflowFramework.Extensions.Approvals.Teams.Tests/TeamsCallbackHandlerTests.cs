using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Teams.Tests;

public sealed class TeamsCallbackHandlerTests
{
    private const string Secret = "handler-test-secret-xyz";

    private static TeamsCallbackTokenService MakeTokenService() =>
        new(Options.Create(new TeamsApprovalOptions { CallbackSharedSecret = Secret }));

    private static (PersistentApprovalService Persistent, IApprovalStore Store) MakePersistentWithStore(
        IApprovalStore? store = null)
    {
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("teams");
        var s = store ?? Substitute.For<IApprovalStore>();
        return (new PersistentApprovalService(inner, s), s);
    }

    private static PendingApproval MakePending(string correlationId, IReadOnlyList<string>? allowedRoles = null) =>
        new(
            CorrelationId: correlationId,
            Request: new ApprovalRequest(
                Title: "Test",
                Description: null,
                Context: new Dictionary<string, object?>(),
                RequiredApprovers: 1,
                Timeout: TimeSpan.FromHours(1),
                AllowedRoles: allowedRoles)
            {
                CorrelationId = correlationId
            },
            PrimaryChannel: "teams",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow.AddHours(1),
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject);

    private static JsonNode BuildPayload(
        string correlationId,
        string decision,
        string token,
        string fromId = "user@tenant",
        string fromName = "Alice")
    {
        return JsonNode.Parse($$"""
        {
            "from": { "id": "{{fromId}}", "name": "{{fromName}}" },
            "value": {
                "correlationId": "{{correlationId}}",
                "decision": "{{decision}}",
                "token": "{{token}}"
            }
        }
        """)!;
    }

    // ------------------------------------------------------------------
    // Valid payloads — ResolveExternalAsync called → 200
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ValidApprove_ResolveCalledAndReturns200()
    {
        var tokens = MakeTokenService();
        var store = Substitute.For<IApprovalStore>();
        var (persistent, _) = MakePersistentWithStore(store);

        var corrId = "corr-approve-ok";
        var pending = MakePending(corrId);

        // AppendVoteAsync returns the pending with the vote appended.
        store.AppendVoteAsync(corrId, Arg.Any<ApprovalRecord>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var vote = new ApprovalRecord("user@tenant", "Alice", true, null, DateTimeOffset.UtcNow, "teams");
                return Task.FromResult(pending with { Votes = new[] { vote } });
            });

        // Rehydrate so that semaphore/TCS are registered.
        persistent.Rehydrate(pending);

        var handler = new TeamsCallbackHandler(tokens, persistent);
        var token = tokens.Create(corrId, true, DateTimeOffset.UtcNow.AddHours(1));
        var payload = BuildPayload(corrId, "approve", token);

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task HandleAsync_ValidReject_ResolveCalledAndReturns200()
    {
        var tokens = MakeTokenService();
        var store = Substitute.For<IApprovalStore>();
        var (persistent, _) = MakePersistentWithStore(store);

        var corrId = "corr-reject-ok";
        var pending = MakePending(corrId);

        store.AppendVoteAsync(corrId, Arg.Any<ApprovalRecord>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var vote = new ApprovalRecord("user@tenant", "Alice", false, null, DateTimeOffset.UtcNow, "teams");
                return Task.FromResult(pending with { Votes = new[] { vote } });
            });

        persistent.Rehydrate(pending);

        var handler = new TeamsCallbackHandler(tokens, persistent);
        var token = tokens.Create(corrId, false, DateTimeOffset.UtcNow.AddHours(1));
        var payload = BuildPayload(corrId, "reject", token);

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.StatusCode.Should().Be(200);
    }

    // ------------------------------------------------------------------
    // Invalid token → 401
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_InvalidToken_Returns401()
    {
        var (persistent, _) = MakePersistentWithStore();
        var handler = new TeamsCallbackHandler(MakeTokenService(), persistent);
        var payload = BuildPayload("corr-x", "approve", "totally.invalid");

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.StatusCode.Should().Be(401);
    }

    // ------------------------------------------------------------------
    // CorrelationId mismatch between token and payload → 401
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_TokenCorrelationIdMismatch_Returns401()
    {
        var tokens = MakeTokenService();
        var (persistent, _) = MakePersistentWithStore();
        var handler = new TeamsCallbackHandler(tokens, persistent);

        // Token encodes "corr-aaa" but payload claims "corr-bbb".
        var token = tokens.Create("corr-aaa", true, DateTimeOffset.UtcNow.AddHours(1));
        var payload = BuildPayload("corr-bbb", "approve", token);

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.StatusCode.Should().Be(401);
    }

    // ------------------------------------------------------------------
    // Decision mismatch between token and payload → 401
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_TokenDecisionMismatch_Returns401()
    {
        var tokens = MakeTokenService();
        var (persistent, _) = MakePersistentWithStore();
        var handler = new TeamsCallbackHandler(tokens, persistent);

        // Token says approve but payload says reject.
        var token = tokens.Create("corr-dm", true, DateTimeOffset.UtcNow.AddHours(1));
        var payload = BuildPayload("corr-dm", "reject", token);

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.StatusCode.Should().Be(401);
    }

    // ------------------------------------------------------------------
    // UnauthorizedAccessException from ResolveExternalAsync → 403
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_UnauthorizedAccessException_Returns403()
    {
        var tokens = MakeTokenService();
        var store = Substitute.For<IApprovalStore>();
        var (persistent, _) = MakePersistentWithStore(store);

        var corrId = "corr-403";
        // AllowedRoles restricts to a specific role; our user is not in it.
        var pending = MakePending(corrId, allowedRoles: new[] { "admin" });

        store.AppendVoteAsync(corrId, Arg.Any<ApprovalRecord>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(pending with { Votes = Array.Empty<ApprovalRecord>() }));

        persistent.Rehydrate(pending);

        var handler = new TeamsCallbackHandler(tokens, persistent);
        var token = tokens.Create(corrId, true, DateTimeOffset.UtcNow.AddHours(1));
        // fromId "nobody@tenant" is not in AllowedRoles["admin"].
        var payload = BuildPayload(corrId, "approve", token, fromId: "nobody@tenant");

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.StatusCode.Should().Be(403);
    }

    // ------------------------------------------------------------------
    // InvalidOperationException from ResolveExternalAsync → 404
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_InvalidOperationException_Returns404()
    {
        var tokens = MakeTokenService();
        var (persistent, _) = MakePersistentWithStore();
        var handler = new TeamsCallbackHandler(tokens, persistent);

        // No Rehydrate call → correlationId not found → InvalidOperationException.
        var token = tokens.Create("corr-404", true, DateTimeOffset.UtcNow.AddHours(1));
        var payload = BuildPayload("corr-404", "approve", token);

        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    // ------------------------------------------------------------------
    // Malformed payload → 400
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_NullPayload_Returns400()
    {
        var (persistent, _) = MakePersistentWithStore();
        var handler = new TeamsCallbackHandler(MakeTokenService(), persistent);

        var result = await handler.HandleAsync(null, CancellationToken.None);

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task HandleAsync_MissingValueField_Returns400()
    {
        var (persistent, _) = MakePersistentWithStore();
        var handler = new TeamsCallbackHandler(MakeTokenService(), persistent);

        var payload = JsonNode.Parse(@"{ ""from"": { ""id"": ""u1"" } }");
        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task HandleAsync_MissingRequiredValueFields_Returns400()
    {
        var (persistent, _) = MakePersistentWithStore();
        var handler = new TeamsCallbackHandler(MakeTokenService(), persistent);

        // value exists but is missing decision and token.
        var payload = JsonNode.Parse(@"{ ""from"": {}, ""value"": { ""correlationId"": ""x"" } }");
        var result = await handler.HandleAsync(payload, CancellationToken.None);

        result.StatusCode.Should().Be(400);
    }
}
