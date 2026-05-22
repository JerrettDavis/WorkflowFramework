using FluentAssertions;
using WorkflowFramework.Extensions.Approvals;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Tests;

public sealed class InMemoryApprovalStoreTests
{
    private static PendingApproval MakePending(string? id = null) =>
        new(
            CorrelationId: id ?? Guid.NewGuid().ToString("N"),
            Request: new ApprovalRequestBuilder()
                .WithTitle("Test")
                .WithTimeout(TimeSpan.FromMinutes(1))
                .Build(),
            PrimaryChannel: "test",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow.AddMinutes(1),
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject);

    private static ApprovalRecord Vote(string approver, bool approved = true) =>
        new(approver, approver, approved, null, DateTimeOffset.UtcNow, "test");

    // ------------------------------------------------------------------
    // SaveAsync / LoadAsync round-trip
    // ------------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_ThenLoad_ReturnsRecord()
    {
        var store = new InMemoryApprovalStore();
        var pending = MakePending("corr-1");

        await store.SaveAsync(pending);
        var loaded = await store.LoadAsync("corr-1");

        loaded.Should().NotBeNull();
        loaded!.CorrelationId.Should().Be("corr-1");
    }

    [Fact]
    public async Task LoadAsync_MissingCorrelation_ReturnsNull()
    {
        var store = new InMemoryApprovalStore();

        var result = await store.LoadAsync("does-not-exist");
        result.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // AppendVoteAsync guard conditions
    // ------------------------------------------------------------------

    [Fact]
    public async Task AppendVoteAsync_MissingCorrelation_ThrowsInvalidOperation()
    {
        var store = new InMemoryApprovalStore();

        var act = async () => await store.AppendVoteAsync("ghost-id", Vote("user1"));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AppendVoteAsync_CompletedApproval_ThrowsInvalidOperation()
    {
        var store = new InMemoryApprovalStore();
        var pending = MakePending("corr-done");
        await store.SaveAsync(pending);
        await store.CompleteAsync("corr-done", ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>()));

        var act = async () => await store.AppendVoteAsync("corr-done", Vote("user1"));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AppendVoteAsync_SameApproverTwice_ReplacesPreviousVote()
    {
        var store = new InMemoryApprovalStore();
        var pending = MakePending("corr-idem");
        await store.SaveAsync(pending);

        await store.AppendVoteAsync("corr-idem", Vote("user1", approved: true));
        var updated = await store.AppendVoteAsync("corr-idem", Vote("user1", approved: false));

        // Only one vote, and it should be the last one (rejected).
        updated.Votes.Should().HaveCount(1);
        updated.Votes[0].Approved.Should().BeFalse();
    }

    [Fact]
    public async Task AppendVoteAsync_Concurrent_DoesNotLoseVotes()
    {
        var store = new InMemoryApprovalStore();
        var pending = MakePending("corr-concurrent");
        await store.SaveAsync(pending);

        const int voterCount = 10;
        var tasks = Enumerable.Range(0, voterCount)
            .Select(i => store.AppendVoteAsync("corr-concurrent", Vote($"user{i}")))
            .ToArray();

        await Task.WhenAll(tasks);

        var loaded = await store.LoadAsync("corr-concurrent");
        loaded!.Votes.Should().HaveCount(voterCount);
    }

    // ------------------------------------------------------------------
    // ListPendingAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task ListPendingAsync_ExcludesCompleted()
    {
        var store = new InMemoryApprovalStore();
        var p1 = MakePending("p1");
        var p2 = MakePending("p2");

        await store.SaveAsync(p1);
        await store.SaveAsync(p2);
        await store.CompleteAsync("p1", ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>()));

        var pending = await store.ListPendingAsync();
        pending.Should().HaveCount(1);
        pending[0].CorrelationId.Should().Be("p2");
    }

    // ------------------------------------------------------------------
    // CompleteAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task CompleteAsync_SetsIsCompleteAndFinal()
    {
        var store = new InMemoryApprovalStore();
        var pending = MakePending("corr-complete");
        await store.SaveAsync(pending);

        var final = ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
        await store.CompleteAsync("corr-complete", final);

        var loaded = await store.LoadAsync("corr-complete");
        loaded!.IsComplete.Should().BeTrue();
        loaded.Final.Should().Be(final);
    }
}
