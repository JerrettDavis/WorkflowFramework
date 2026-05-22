using System.CommandLine;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals.Cli.Commands;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Cli.Tests;

/// <summary>
/// Integration-style tests for <see cref="WfApprovalsCommand"/> using real
/// <see cref="InMemoryApprovalStore"/> and <see cref="PersistentApprovalService"/> instances
/// with a hanging mock inner channel. Commands are invoked via
/// <c>Command.InvokeAsync</c> to exercise the full System.CommandLine pipeline.
/// </summary>
public sealed class WfApprovalsCommandTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ApprovalRequest MakeRequest(
        string title = "Deploy to Production",
        int required = 1,
        TimeSpan? timeout = null,
        string[]? allowedRoles = null,
        IReadOnlyDictionary<string, object?>? context = null)
    {
        var builder = new ApprovalRequestBuilder()
            .WithTitle(title)
            .WithDescription("Deploy v2.0 to production environment.")
            .WithTimeout(timeout ?? TimeSpan.FromSeconds(30))
            .RequiringApprovers(required);

        if (allowedRoles is not null)
            builder = builder.AllowedFor(allowedRoles);

        if (context is not null)
            foreach (var kv in context)
                builder = builder.WithContext(kv.Key, kv.Value);

        return builder.Build();
    }

    private static ApprovalRecord Vote(string id, bool approved = true, string? comment = null, string? displayName = null) =>
        new(id, displayName, approved, comment, DateTimeOffset.UtcNow, "cli");

    /// <summary>
    /// Sets up a real store + persistent service and seeds a pending approval into both.
    /// Returns the store, persistent service, and seeded pending approval.
    /// </summary>
    private static async Task<(InMemoryApprovalStore store, PersistentApprovalService persistent, PendingApproval pending)>
        SeedAsync(ApprovalRequest? requestOverride = null)
    {
        var store = new InMemoryApprovalStore();

        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("cli");
        inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(Timeout.Infinite, ci.Arg<CancellationToken>());
                return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
            });

        var persistent = new PersistentApprovalService(inner, store);
        var request = requestOverride ?? MakeRequest();

        // Seed via RequestApprovalAsync to register TCS in persistent.
        _ = Task.Run(() => persistent.RequestApprovalAsync(request));
        await Task.Delay(50); // Let it persist.

        var loaded = await store.LoadAsync(request.CorrelationId);
        return (store, persistent, loaded!);
    }

    private static Command BuildApprovalsCommand(
        InMemoryApprovalStore store,
        PersistentApprovalService persistent,
        TestConsole console)
        => WfApprovalsCommand.Build(store, persistent, console);

    // -------------------------------------------------------------------------
    // list — empty store
    // -------------------------------------------------------------------------

    [Fact]
    public async Task List_EmptyStore_PrintsNoApprovals()
    {
        var store = new InMemoryApprovalStore();
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("cli");
        var persistent = new PersistentApprovalService(inner, store);
        var console = new TestConsole();

        var cmd = BuildApprovalsCommand(store, persistent, console);
        var exitCode = await cmd.InvokeAsync("list");

        exitCode.Should().Be(0);
        console.Output.Should().Contain("No pending approvals");
    }

    // -------------------------------------------------------------------------
    // list — two pending rows
    // -------------------------------------------------------------------------

    [Fact]
    public async Task List_TwoPending_PrintsBothRows()
    {
        var (store, persistent, _) = await SeedAsync(MakeRequest("Request One"));
        var (_, _, _) = await SeedAsync(MakeRequest("Request Two"));

        // Need shared store — re-seed both into same store.
        var sharedStore = new InMemoryApprovalStore();
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("cli");
        inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(Timeout.Infinite, ci.Arg<CancellationToken>());
                return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
            });
        var sharedPersistent = new PersistentApprovalService(inner, sharedStore);

        var req1 = MakeRequest("Request One");
        var req2 = MakeRequest("Request Two");

        _ = Task.Run(() => sharedPersistent.RequestApprovalAsync(req1));
        _ = Task.Run(() => sharedPersistent.RequestApprovalAsync(req2));
        await Task.Delay(80);

        var console = new TestConsole();
        var cmd = BuildApprovalsCommand(sharedStore, sharedPersistent, console);
        var exitCode = await cmd.InvokeAsync("list");

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Request One");
        console.Output.Should().Contain("Request Two");

        // Cleanup
        sharedPersistent.DirectComplete(req1.CorrelationId, ApprovalResponse.TimedOut(Array.Empty<ApprovalRecord>()));
        sharedPersistent.DirectComplete(req2.CorrelationId, ApprovalResponse.TimedOut(Array.Empty<ApprovalRecord>()));
    }

    // -------------------------------------------------------------------------
    // show — missing correlation ID
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Show_MissingId_ReturnsExitCode2()
    {
        var store = new InMemoryApprovalStore();
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("cli");
        var persistent = new PersistentApprovalService(inner, store);
        var console = new TestConsole();

        var cmd = BuildApprovalsCommand(store, persistent, console);
        var exitCode = await cmd.InvokeAsync("show nonexistent-id");

        exitCode.Should().Be(2);
        console.Output.Should().Contain("not found");
    }

    // -------------------------------------------------------------------------
    // show — existing, prints title + description + context + votes
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Show_ExistingId_PrintsDetails()
    {
        var context = new Dictionary<string, object?> { ["commit"] = "abc123", ["env"] = "prod" };
        var request = MakeRequest(title: "Deploy v2", context: context);
        var (store, persistent, _) = await SeedAsync(request);
        var console = new TestConsole();

        var cmd = BuildApprovalsCommand(store, persistent, console);
        var exitCode = await cmd.InvokeAsync($"show {request.CorrelationId}");

        exitCode.Should().Be(0);
        var output = console.Output;
        output.Should().Contain("Deploy v2");
        output.Should().Contain("Deploy v2.0 to production environment.");
        output.Should().Contain("commit");
        output.Should().Contain("abc123");
        output.Should().Contain("env");
        output.Should().Contain("prod");
        output.Should().Contain("no votes");

        // Cleanup
        persistent.DirectComplete(request.CorrelationId, ApprovalResponse.TimedOut(Array.Empty<ApprovalRecord>()));
    }

    // -------------------------------------------------------------------------
    // approve — happy path (1-of-1 quorum)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Approve_ValidId_RecordsVoteAndReturnsExitCode0()
    {
        var request = MakeRequest(required: 1);
        var (store, persistent, _) = await SeedAsync(request);
        var console = new TestConsole();

        var cmd = BuildApprovalsCommand(store, persistent, console);

        // Approve resolves the pending TCS, so launch it concurrently.
        var exitCode = await cmd.InvokeAsync($"approve {request.CorrelationId} --by user1");

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Approved by user1");

        // Verify the vote was appended to the store.
        var loaded = await store.LoadAsync(request.CorrelationId);
        loaded!.Votes.Should().ContainSingle(v => v.ApproverId == "user1" && v.Approved);
    }

    // -------------------------------------------------------------------------
    // approve — 2-of-2 quorum requires two invocations
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Approve_TwoOfTwoQuorum_BothVotesRecorded_CompletesApproved()
    {
        var request = MakeRequest(required: 2, allowedRoles: new[] { "u1", "u2" });
        var (store, persistent, _) = await SeedAsync(request);
        var console = new TestConsole();

        var cmd = BuildApprovalsCommand(store, persistent, console);

        // First vote (quorum not yet reached).
        var exit1 = await cmd.InvokeAsync($"approve {request.CorrelationId} --by u1");
        exit1.Should().Be(0);

        // Second vote (quorum reached — TCS will be set).
        var completionTask = persistent.WaitForCompletionAsync(request.CorrelationId);
        var exit2 = await cmd.InvokeAsync($"approve {request.CorrelationId} --by u2");
        exit2.Should().Be(0);

        var result = await completionTask.WaitAsync(TimeSpan.FromSeconds(5));
        result.Approved.Should().BeTrue();
        result.Outcome.Should().Be(ApprovalOutcome.Approved);
        result.Approvals.Should().HaveCount(2);
    }

    // -------------------------------------------------------------------------
    // reject — happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Reject_ValidId_RecordsRejectionAndReturnsExitCode0()
    {
        var request = MakeRequest(required: 1);
        var (store, persistent, _) = await SeedAsync(request);
        var console = new TestConsole();

        var cmd = BuildApprovalsCommand(store, persistent, console);
        var exitCode = await cmd.InvokeAsync($"reject {request.CorrelationId} --by user1 --comment \"Not ready\"");

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Rejected by user1");

        var loaded = await store.LoadAsync(request.CorrelationId);
        loaded!.Votes.Should().ContainSingle(v => v.ApproverId == "user1" && !v.Approved);
    }

    // -------------------------------------------------------------------------
    // approve — --by missing → System.CommandLine validation error, non-zero exit
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Approve_MissingByOption_ReturnsNonZeroExitCode()
    {
        var store = new InMemoryApprovalStore();
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("cli");
        var persistent = new PersistentApprovalService(inner, store);
        var console = new TestConsole();

        var cmd = BuildApprovalsCommand(store, persistent, console);

        // --by is required; omitting it should cause a parse error.
        var exitCode = await cmd.InvokeAsync("approve some-correlation-id");

        exitCode.Should().NotBe(0);
    }

    // -------------------------------------------------------------------------
    // approve — unauthorized voter → exit 3
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Approve_UnauthorizedVoter_ReturnsExitCode3()
    {
        var request = MakeRequest(required: 1, allowedRoles: new[] { "sre" });
        var (store, persistent, _) = await SeedAsync(request);
        var console = new TestConsole();

        var cmd = BuildApprovalsCommand(store, persistent, console);
        var exitCode = await cmd.InvokeAsync($"approve {request.CorrelationId} --by other-user");

        exitCode.Should().Be(3);
        console.Output.Should().Contain("Unauthorized");

        // Cleanup: resolve the background RequestApprovalAsync task so the test doesn't hang.
        // Ignore ObjectDisposedException — the semaphore may already be cleaned up under
        // fast CI schedulers (seen on net9.0 under high-parallelism runs).
        try
        {
            await persistent.ResolveExternalAsync(
                request.CorrelationId,
                new ApprovalRecord("sre", null, true, null, DateTimeOffset.UtcNow, "cli"));
        }
        catch (ObjectDisposedException) { /* semaphore already cleaned up — safe to ignore */ }
    }

    // -------------------------------------------------------------------------
    // approve — missing correlationId in store → exit 2
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Approve_CorrelationIdNotFound_ReturnsExitCode2()
    {
        var store = new InMemoryApprovalStore();
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("cli");
        var persistent = new PersistentApprovalService(inner, store);
        var console = new TestConsole();

        var cmd = BuildApprovalsCommand(store, persistent, console);
        var exitCode = await cmd.InvokeAsync("approve nonexistent-id --by user1");

        exitCode.Should().Be(2);
        console.Output.Should().Contain("not found");
    }

    // -------------------------------------------------------------------------
    // approve — --comment captured in vote
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Approve_WithComment_CommentCapturedInVote()
    {
        var request = MakeRequest(required: 1);
        var (store, persistent, _) = await SeedAsync(request);
        var console = new TestConsole();

        var cmd = BuildApprovalsCommand(store, persistent, console);
        await cmd.InvokeAsync($"approve {request.CorrelationId} --by user1 --comment LGTM");

        var loaded = await store.LoadAsync(request.CorrelationId);
        loaded!.Votes.Should().ContainSingle(v => v.Comment == "LGTM");
    }

    // -------------------------------------------------------------------------
    // approve — --name captured in vote display name
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Approve_WithName_DisplayNameCapturedInVote()
    {
        var request = MakeRequest(required: 1);
        var (store, persistent, _) = await SeedAsync(request);
        var console = new TestConsole();

        var cmd = BuildApprovalsCommand(store, persistent, console);
        await cmd.InvokeAsync($"approve {request.CorrelationId} --by user1 --name \"Alice Smith\"");

        var loaded = await store.LoadAsync(request.CorrelationId);
        loaded!.Votes.Should().ContainSingle(v => v.ApproverDisplayName == "Alice Smith");
    }

    // -------------------------------------------------------------------------
    // reject — --name and --comment captured in vote
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Reject_WithNameAndComment_CapturedInVote()
    {
        var request = MakeRequest(required: 1);
        var (store, persistent, _) = await SeedAsync(request);
        var console = new TestConsole();

        var cmd = BuildApprovalsCommand(store, persistent, console);
        await cmd.InvokeAsync($"reject {request.CorrelationId} --by bob --name \"Bob Jones\" --comment \"Not ready yet\"");

        var loaded = await store.LoadAsync(request.CorrelationId);
        var vote = loaded!.Votes.Should().ContainSingle().Subject;
        vote.ApproverId.Should().Be("bob");
        vote.ApproverDisplayName.Should().Be("Bob Jones");
        vote.Comment.Should().Be("Not ready yet");
        vote.Approved.Should().BeFalse();
    }
}
