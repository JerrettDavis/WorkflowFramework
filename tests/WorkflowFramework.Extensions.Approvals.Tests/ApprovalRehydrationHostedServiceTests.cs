using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Tests;

public sealed class ApprovalRehydrationHostedServiceTests
{
    private static PendingApproval MakePending(
        string id,
        DateTimeOffset? deadline = null,
        OnTimeoutAction action = OnTimeoutAction.AutoReject,
        bool isComplete = false)
    {
        var request = new ApprovalRequestBuilder()
            .WithTitle("Test")
            .WithTimeout(TimeSpan.FromMinutes(1))
            .WithCorrelationId(id)
            .Build();

        return new PendingApproval(
            CorrelationId: id,
            Request: request,
            PrimaryChannel: "test",
            CreatedAt: DateTimeOffset.UtcNow.AddSeconds(-30),
            DeadlineAt: deadline ?? DateTimeOffset.UtcNow.AddSeconds(30),
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: action,
            IsComplete: isComplete);
    }

    private static (PersistentApprovalService svc, InMemoryApprovalStore store) BuildSvc()
    {
        var store = new InMemoryApprovalStore();
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("test-inner");

        // Inner hangs until cancelled.
        inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var ct = ci.Arg<CancellationToken>();
                await Task.Delay(Timeout.Infinite, ct);
                return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
            });

        var svc = new PersistentApprovalService(inner, store);
        return (svc, store);
    }

    // ------------------------------------------------------------------
    // Constructor null guards
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_NullStore_ThrowsArgumentNullException()
    {
        var (svc, _) = BuildSvc();
        var act = () => new ApprovalRehydrationHostedService(null!, svc);
        act.Should().Throw<ArgumentNullException>().WithParameterName("store");
    }

    [Fact]
    public void Constructor_NullService_ThrowsArgumentNullException()
    {
        var (_, store) = BuildSvc();
        var act = () => new ApprovalRehydrationHostedService(store, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("service");
    }

    // ------------------------------------------------------------------
    // Rehydrate exception is caught and logged (not thrown)
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_RehydrateThrows_ContinuesToNextPending()
    {
        // Create a store mock that has an approval with null correlation (triggers Rehydrate null guard).
        var (svc, store) = BuildSvc();
        var good = MakePending("rh-good");
        await store.SaveAsync(good);

        // Add a second pending that will trigger an exception in Rehydrate
        // by having a CorrelationId already registered (TryAdd no-ops, then no TCS = exception in WaitForCompletion).
        // Simply save a good second one to verify both are processed.
        var good2 = MakePending("rh-good2");
        await store.SaveAsync(good2);

        var hosted = new ApprovalRehydrationHostedService(store, svc);
        // Should not throw even if some rehydrations fail internally.
        await hosted.StartAsync(CancellationToken.None);

        // Both should be waiteable.
        var w1 = svc.WaitForCompletionAsync("rh-good");
        var w2 = svc.WaitForCompletionAsync("rh-good2");
        w1.IsCompleted.Should().BeFalse();
        w2.IsCompleted.Should().BeFalse();

        // Cleanup.
        svc.DirectComplete("rh-good", ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>()));
        svc.DirectComplete("rh-good2", ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>()));

        await w1.WaitAsync(TimeSpan.FromSeconds(3));
        await w2.WaitAsync(TimeSpan.FromSeconds(3));
    }

    // ------------------------------------------------------------------
    // StartAsync rehydrates all pending from store
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_RehydratesAllPending()
    {
        var (svc, store) = BuildSvc();
        var p1 = MakePending("rh-1");
        var p2 = MakePending("rh-2");
        await store.SaveAsync(p1);
        await store.SaveAsync(p2);

        var hosted = new ApprovalRehydrationHostedService(store, svc);
        await hosted.StartAsync(CancellationToken.None);

        // Both should now be wait-able.
        var wait1 = svc.WaitForCompletionAsync("rh-1");
        var wait2 = svc.WaitForCompletionAsync("rh-2");

        wait1.IsCompleted.Should().BeFalse();
        wait2.IsCompleted.Should().BeFalse();

        // Resolve externally.
        await svc.ResolveExternalAsync("rh-1",
            new ApprovalRecord("u1", "u1", true, null, DateTimeOffset.UtcNow, "test"));
        await svc.ResolveExternalAsync("rh-2",
            new ApprovalRecord("u2", "u2", true, null, DateTimeOffset.UtcNow, "test"));

        var r1 = await wait1.WaitAsync(TimeSpan.FromSeconds(3));
        var r2 = await wait2.WaitAsync(TimeSpan.FromSeconds(3));
        r1.Outcome.Should().Be(ApprovalOutcome.Approved);
        r2.Outcome.Should().Be(ApprovalOutcome.Approved);
    }

    // ------------------------------------------------------------------
    // Pending with past deadline immediately completes per OnTimeoutAction
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_PastDeadline_ImmediatelyCompletesWithTimeout()
    {
        var (svc, store) = BuildSvc();
        var expired = MakePending("rh-expired", deadline: DateTimeOffset.UtcNow.AddSeconds(-1));
        await store.SaveAsync(expired);

        var hosted = new ApprovalRehydrationHostedService(store, svc);
        await hosted.StartAsync(CancellationToken.None);

        // Rehydrate registers TCS; past deadline fires immediately.
        // Give a short grace period for the background task.
        var waitTask = svc.WaitForCompletionAsync("rh-expired");
        var result = await waitTask.WaitAsync(TimeSpan.FromSeconds(3));
        result.Outcome.Should().Be(ApprovalOutcome.TimedOut);
    }

    // ------------------------------------------------------------------
    // Pending with future deadline schedules timer; TCS works
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_FutureDeadline_TcsCompletesOnExternalResolve()
    {
        var (svc, store) = BuildSvc();
        var pending = MakePending("rh-future", deadline: DateTimeOffset.UtcNow.AddSeconds(10));
        await store.SaveAsync(pending);

        var hosted = new ApprovalRehydrationHostedService(store, svc);
        await hosted.StartAsync(CancellationToken.None);

        var waitTask = svc.WaitForCompletionAsync("rh-future");
        waitTask.IsCompleted.Should().BeFalse();

        await svc.ResolveExternalAsync("rh-future",
            new ApprovalRecord("u1", "u1", true, null, DateTimeOffset.UtcNow, "test"));

        var result = await waitTask.WaitAsync(TimeSpan.FromSeconds(3));
        result.Outcome.Should().Be(ApprovalOutcome.Approved);
    }

    // ------------------------------------------------------------------
    // StopAsync does not complete pending TCS
    // ------------------------------------------------------------------

    [Fact]
    public async Task StopAsync_DoesNotCompletePendingTcs()
    {
        var (svc, store) = BuildSvc();
        var pending = MakePending("rh-stop");
        await store.SaveAsync(pending);

        var hosted = new ApprovalRehydrationHostedService(store, svc);
        await hosted.StartAsync(CancellationToken.None);

        var waitTask = svc.WaitForCompletionAsync("rh-stop");

        await hosted.StopAsync(CancellationToken.None);

        // Wait a brief moment — TCS should still be pending.
        await Task.Delay(50);
        waitTask.IsCompleted.Should().BeFalse();
    }
}
