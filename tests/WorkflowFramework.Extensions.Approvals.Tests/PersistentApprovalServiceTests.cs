using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Tests;

public sealed class PersistentApprovalServiceTests
{
    private static ApprovalRequest MakeRequest(
        int required = 1,
        TimeSpan? timeout = null,
        string[]? allowedRoles = null)
    {
        var builder = new ApprovalRequestBuilder()
            .WithTitle("Deploy")
            .WithTimeout(timeout ?? TimeSpan.FromSeconds(5))
            .RequiringApprovers(required);

        if (allowedRoles is not null)
            builder = builder.AllowedFor(allowedRoles);

        return builder.Build();
    }

    private static ApprovalRecord Vote(string id, bool approved = true) =>
        new(id, id, approved, null, DateTimeOffset.UtcNow, "test");

    private static (PersistentApprovalService, InMemoryApprovalStore, IApprovalChannel) Build(
        Func<ApprovalRequest, CancellationToken, Task<ApprovalResponse>>? innerBehavior = null)
    {
        var store = new InMemoryApprovalStore();
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("test-inner");

        if (innerBehavior is not null)
        {
            inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
                .Returns(ci => innerBehavior(ci.Arg<ApprovalRequest>(), ci.Arg<CancellationToken>()));
        }
        else
        {
            // Default: hang until cancelled (external resolution drives completions).
            inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
                .Returns(async ci =>
                {
                    var ct = ci.Arg<CancellationToken>();
                    await Task.Delay(Timeout.Infinite, ct);
                    return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
                });
        }

        var svc = new PersistentApprovalService(inner, store);
        return (svc, store, inner);
    }

    // ------------------------------------------------------------------
    // RequestApprovalAsync persists PendingApproval to store
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_PersistsToStore()
    {
        var (svc, store, _) = Build();
        var request = MakeRequest();

        var task = svc.RequestApprovalAsync(request);
        await Task.Delay(30); // let it persist before checking

        var loaded = await store.LoadAsync(request.CorrelationId);
        loaded.Should().NotBeNull();
        loaded!.CorrelationId.Should().Be(request.CorrelationId);

        // Resolve to clean up.
        await svc.ResolveExternalAsync(request.CorrelationId, Vote("u1"));
        await task;
    }

    // ------------------------------------------------------------------
    // ResolveExternalAsync with non-quorum vote leaves request pending
    // ------------------------------------------------------------------

    [Fact]
    public async Task ResolveExternalAsync_NonQuorum_LeavesRequestPending()
    {
        var (svc, store, _) = Build();
        var request = MakeRequest(required: 2);

        var requestTask = svc.RequestApprovalAsync(request);
        await Task.Delay(30);

        await svc.ResolveExternalAsync(request.CorrelationId, Vote("u1"));

        requestTask.IsCompleted.Should().BeFalse();

        // Cleanup
        await svc.ResolveExternalAsync(request.CorrelationId, Vote("u2"));
        await requestTask;
    }

    // ------------------------------------------------------------------
    // ResolveExternalAsync that reaches quorum completes TCS
    // ------------------------------------------------------------------

    [Fact]
    public async Task ResolveExternalAsync_QuorumReached_CompletesAsApproved()
    {
        var (svc, _, _) = Build();
        var request = MakeRequest(required: 2);

        var requestTask = svc.RequestApprovalAsync(request);
        await Task.Delay(30);

        await svc.ResolveExternalAsync(request.CorrelationId, Vote("u1"));
        await svc.ResolveExternalAsync(request.CorrelationId, Vote("u2"));

        var result = await requestTask.WaitAsync(TimeSpan.FromSeconds(3));
        result.Outcome.Should().Be(ApprovalOutcome.Approved);
        result.Approved.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // ResolveExternalAsync with rejection that short-circuits
    // ------------------------------------------------------------------

    [Fact]
    public async Task ResolveExternalAsync_QuorumImpossible_CompletesAsRejected()
    {
        // 2-of-2 required, one rejection makes quorum impossible.
        var (svc, _, _) = Build();
        var request = MakeRequest(required: 2, allowedRoles: new[] { "u1", "u2" });

        var requestTask = svc.RequestApprovalAsync(request);
        await Task.Delay(30);

        await svc.ResolveExternalAsync(request.CorrelationId, Vote("u1", approved: false));

        var result = await requestTask.WaitAsync(TimeSpan.FromSeconds(3));
        result.Outcome.Should().Be(ApprovalOutcome.Rejected);
        result.Approved.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Vote outside AllowedRoles throws UnauthorizedAccessException
    // ------------------------------------------------------------------

    [Fact]
    public async Task ResolveExternalAsync_VoterNotInAllowedRoles_ThrowsUnauthorized()
    {
        var (svc, _, _) = Build();
        var request = MakeRequest(required: 1, allowedRoles: new[] { "allowed-user" });

        var requestTask = svc.RequestApprovalAsync(request);
        await Task.Delay(30);

        var act = async () => await svc.ResolveExternalAsync(
            request.CorrelationId, Vote("hacker", approved: true));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        // Cleanup (resolve with allowed user).
        await svc.ResolveExternalAsync(request.CorrelationId, Vote("allowed-user"));
        await requestTask;
    }

    // ------------------------------------------------------------------
    // WaitForCompletionAsync rehydration
    // ------------------------------------------------------------------

    [Fact]
    public async Task WaitForCompletionAsync_CompletesWhenResolveExternalFires()
    {
        var (svc, _, _) = Build();
        var request = MakeRequest();

        var requestTask = svc.RequestApprovalAsync(request);
        await Task.Delay(30);

        var waitTask = svc.WaitForCompletionAsync(request.CorrelationId);

        await svc.ResolveExternalAsync(request.CorrelationId, Vote("u1"));

        var result = await waitTask.WaitAsync(TimeSpan.FromSeconds(3));
        result.Outcome.Should().Be(ApprovalOutcome.Approved);

        await requestTask;
    }

    // ------------------------------------------------------------------
    // Deadline timer fires TimedOut response
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_DeadlineFires_ReturnsTimedOut()
    {
        var (svc, _, _) = Build();
        var request = MakeRequest(timeout: TimeSpan.FromMilliseconds(100));

        var result = await svc.RequestApprovalAsync(request)
            .WaitAsync(TimeSpan.FromSeconds(5));

        result.Outcome.Should().Be(ApprovalOutcome.TimedOut);
        result.Approved.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Concurrent ResolveExternalAsync calls serialize (no race on quorum)
    // ------------------------------------------------------------------

    [Fact]
    public async Task ResolveExternalAsync_ConcurrentVotes_SerializeCorrectly()
    {
        var (svc, _, _) = Build();
        // 5 required, 5 distinct voters — all approvals, should reach quorum exactly once.
        var request = MakeRequest(required: 5, allowedRoles: new[] { "u1", "u2", "u3", "u4", "u5" });

        var requestTask = svc.RequestApprovalAsync(request);
        await Task.Delay(30);

        var voteTasks = new[]
        {
            svc.ResolveExternalAsync(request.CorrelationId, Vote("u1")),
            svc.ResolveExternalAsync(request.CorrelationId, Vote("u2")),
            svc.ResolveExternalAsync(request.CorrelationId, Vote("u3")),
            svc.ResolveExternalAsync(request.CorrelationId, Vote("u4")),
            svc.ResolveExternalAsync(request.CorrelationId, Vote("u5")),
        };

        await Task.WhenAll(voteTasks);

        var result = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
        result.Outcome.Should().Be(ApprovalOutcome.Approved);
    }

    // ------------------------------------------------------------------
    // RequestApprovalAsync validation guards
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_RequiredApproversZero_ThrowsArgumentException()
    {
        var (svc, _, _) = Build();
        // Construct directly bypassing builder validation to hit service-level guard
        var request = new ApprovalRequest(
            Title: "Test",
            Description: null,
            Context: new Dictionary<string, object?>(),
            RequiredApprovers: 0, // invalid — below service guard threshold of 1
            Timeout: TimeSpan.FromSeconds(5),
            AllowedRoles: null);

        var act = async () => await svc.RequestApprovalAsync(request);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RequestApprovalAsync_TimeoutZero_ThrowsArgumentException()
    {
        var (svc, _, _) = Build();
        // Construct directly bypassing builder validation to hit service-level guard
        var request = new ApprovalRequest(
            Title: "Test",
            Description: null,
            Context: new Dictionary<string, object?>(),
            RequiredApprovers: 1,
            Timeout: TimeSpan.Zero, // invalid
            AllowedRoles: null);

        var act = async () => await svc.RequestApprovalAsync(request);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RequestApprovalAsync_EmptyTitle_ThrowsArgumentException()
    {
        var (svc, _, _) = Build();
        var request = new ApprovalRequest(
            Title: "   ", // whitespace only — invalid
            Description: null,
            Context: new Dictionary<string, object?>(),
            RequiredApprovers: 1,
            Timeout: TimeSpan.FromSeconds(5),
            AllowedRoles: null);

        var act = async () => await svc.RequestApprovalAsync(request);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ------------------------------------------------------------------
    // ResolveExternalAsync - no correlation found throws
    // ------------------------------------------------------------------

    [Fact]
    public async Task ResolveExternalAsync_NoInflightCorrelation_ThrowsInvalidOperation()
    {
        var (svc, _, _) = Build();
        var act = async () => await svc.ResolveExternalAsync("ghost", Vote("user1"));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ghost*");
    }

    // ------------------------------------------------------------------
    // WaitForCompletionAsync - no correlation found throws
    // ------------------------------------------------------------------

    [Fact]
    public async Task WaitForCompletionAsync_NoInflightCorrelation_ThrowsInvalidOperation()
    {
        var (svc, _, _) = Build();
        var act = async () => await svc.WaitForCompletionAsync("ghost");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ghost*");
    }

    // ------------------------------------------------------------------
    // DirectComplete - no-op when correlation not found
    // ------------------------------------------------------------------

    [Fact]
    public void DirectComplete_NoInflightCorrelation_IsNoOp()
    {
        var (svc, _, _) = Build();
        // Should not throw even when correlation doesn't exist.
        svc.DirectComplete("ghost", ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>()));
    }

    // ------------------------------------------------------------------
    // Rehydrate - skips completed approvals
    // ------------------------------------------------------------------

    [Fact]
    public async Task Rehydrate_CompletedApproval_ReturnsEarlyWithoutRegisteringTcs()
    {
        var (svc, _, _) = Build();
        var request = MakeRequest();
        var completed = new PendingApproval(
            CorrelationId: request.CorrelationId,
            Request: request,
            PrimaryChannel: "test",
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            DeadlineAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject,
            IsComplete: true);

        // Should not throw; TCS should NOT be registered.
        svc.Rehydrate(completed);

        // WaitForCompletion should throw since no TCS was registered.
        var act = async () => await svc.WaitForCompletionAsync(request.CorrelationId);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ------------------------------------------------------------------
    // Rehydrate - null guard
    // ------------------------------------------------------------------

    [Fact]
    public void Rehydrate_NullPending_ThrowsArgumentNullException()
    {
        var (svc, _, _) = Build();
        var act = () => svc.Rehydrate(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("pending");
    }

    // ------------------------------------------------------------------
    // Completion calls IApprovalStore.CompleteAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_OnCompletion_CallsStoreCompleteAsync()
    {
        var store = Substitute.For<IApprovalStore>();
        store.SaveAsync(Arg.Any<PendingApproval>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        store.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PendingApproval?)null);

        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("mock");
        inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(Timeout.Infinite, ci.Arg<CancellationToken>());
                return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
            });

        // AppendVoteAsync returns approved snapshot.
        store.AppendVoteAsync(Arg.Any<string>(), Arg.Any<ApprovalRecord>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var votes = new List<ApprovalRecord> { ci.Arg<ApprovalRecord>() };
                var request = new ApprovalRequestBuilder()
                    .WithTitle("Deploy")
                    .WithTimeout(TimeSpan.FromSeconds(5))
                    .RequiringApprovers(1)
                    .Build();
                var pending = new PendingApproval(
                    ci.Arg<string>(), request, "mock",
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(5),
                    votes.AsReadOnly(), null, OnTimeoutAction.AutoReject);
                return Task.FromResult(pending);
            });

        store.CompleteAsync(Arg.Any<string>(), Arg.Any<ApprovalResponse>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var svc = new PersistentApprovalService(inner, store);
        var request = new ApprovalRequestBuilder()
            .WithTitle("Deploy")
            .WithTimeout(TimeSpan.FromSeconds(5))
            .Build();

        var requestTask = svc.RequestApprovalAsync(request);
        await Task.Delay(30);

        await svc.ResolveExternalAsync(request.CorrelationId,
            new ApprovalRecord("u1", "u1", true, null, DateTimeOffset.UtcNow, "test"));

        await requestTask.WaitAsync(TimeSpan.FromSeconds(3));

        await store.Received(1).CompleteAsync(
            request.CorrelationId,
            Arg.Any<ApprovalResponse>(),
            Arg.Any<CancellationToken>());
    }
}
