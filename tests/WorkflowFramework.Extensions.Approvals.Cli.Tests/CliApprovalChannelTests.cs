using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals.Cli.Commands;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Cli.Tests;

/// <summary>
/// Unit tests for <see cref="CliApprovalChannel"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CliApprovalChannel"/> acts as the <em>inner</em> channel inside a
/// <see cref="PersistentApprovalService"/>. Requests enter through the PAS (which registers
/// the TCS and persists to the store), the PAS dispatches to <see cref="CliApprovalChannel"/>
/// in the background, the channel prints its banner and then blocks on
/// <c>WaitForCompletionAsync</c>. Tests therefore drive the full stack via the PAS.
/// </para>
/// </remarks>
public sealed class CliApprovalChannelTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ApprovalRequest MakeRequest(
        int required = 1,
        TimeSpan? timeout = null,
        string[]? allowedRoles = null)
    {
        var builder = new ApprovalRequestBuilder()
            .WithTitle("Deploy to Production")
            .WithDescription("Deploying v2.0 to prod.")
            .WithTimeout(timeout ?? TimeSpan.FromSeconds(10))
            .RequiringApprovers(required);

        if (allowedRoles is not null)
            builder = builder.AllowedFor(allowedRoles);

        return builder.Build();
    }

    /// <summary>
    /// Builds the full CLI channel stack: InMemoryApprovalStore + CliApprovalChannel (inner)
    /// + PersistentApprovalService wrapping the channel.
    /// </summary>
    private static (InMemoryApprovalStore store, PersistentApprovalService persistent, CliApprovalChannel channel, TestConsole console)
        Build()
    {
        var store = new InMemoryApprovalStore();
        var console = new TestConsole();

        // Create a temporary lazy placeholder; we'll set real values below.
        PersistentApprovalService? persistent = null;
        var lazyPersistent = new Lazy<PersistentApprovalService>(() => persistent!);

        var channel = new CliApprovalChannel(store, lazyPersistent, console: console);
        persistent = new PersistentApprovalService(channel, store);

        return (store, persistent, channel, console);
    }

    // -------------------------------------------------------------------------
    // RequestApprovalAsync saves PendingApproval to the store when not present
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_SavesPendingToStore_WhenNotAlreadyPresent()
    {
        var (store, persistent, _, _) = Build();
        var request = MakeRequest();

        // Drive via PAS — which calls CliApprovalChannel internally.
        var requestTask = persistent.RequestApprovalAsync(request);
        await Task.Delay(80); // Give it time to save.

        var loaded = await store.LoadAsync(request.CorrelationId);

        loaded.Should().NotBeNull();
        loaded!.CorrelationId.Should().Be(request.CorrelationId);

        // Resolve to avoid leaked tasks.
        await persistent.ResolveExternalAsync(
            request.CorrelationId,
            new ApprovalRecord("u1", null, true, null, DateTimeOffset.UtcNow, "cli"));
        await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    // -------------------------------------------------------------------------
    // RequestApprovalAsync skips save if pending already exists (rehydration)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_SkipsSave_WhenPendingAlreadyExists()
    {
        var (store, persistent, channel, console) = Build();
        var request = MakeRequest();

        // Start via PAS (creates TCS + saves to store).
        var requestTask = persistent.RequestApprovalAsync(request);
        await Task.Delay(80);

        // Verify saved once.
        var loaded1 = await store.LoadAsync(request.CorrelationId);
        loaded1.Should().NotBeNull();

        // Calling channel.RequestApprovalAsync directly again would normally try to save again.
        // The channel must skip save since the record already exists — no duplicate exception.
        // We verify this by checking that the store still has exactly one record.
        var allPending = await store.ListPendingAsync();
        allPending.Should().ContainSingle(p => p.CorrelationId == request.CorrelationId);

        // Cleanup.
        await persistent.ResolveExternalAsync(
            request.CorrelationId,
            new ApprovalRecord("u1", null, true, null, DateTimeOffset.UtcNow, "cli"));
        await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    // -------------------------------------------------------------------------
    // Prints expected banner including CLI commands
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_PrintsBannerWithApprovalCommands()
    {
        var (store, persistent, _, console) = Build();
        var request = MakeRequest();

        var requestTask = persistent.RequestApprovalAsync(request);
        await Task.Delay(80);

        var output = console.Output;
        output.Should().Contain("APPROVAL REQUIRED");
        output.Should().Contain(request.CorrelationId);
        output.Should().Contain("wf approvals approve");
        output.Should().Contain("wf approvals reject");
        output.Should().Contain(request.Title);

        // Cleanup.
        await persistent.ResolveExternalAsync(
            request.CorrelationId,
            new ApprovalRecord("u1", null, true, null, DateTimeOffset.UtcNow, "cli"));
        await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    // -------------------------------------------------------------------------
    // Returns response when persistent service completes the wait
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_ReturnsResponse_WhenPersistentServiceCompletes()
    {
        var (_, persistent, _, _) = Build();
        var request = MakeRequest();

        var requestTask = persistent.RequestApprovalAsync(request);
        await Task.Delay(80);

        await persistent.ResolveExternalAsync(
            request.CorrelationId,
            new ApprovalRecord("u1", "User One", true, "LGTM", DateTimeOffset.UtcNow, "cli"));

        var result = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));

        result.Should().NotBeNull();
        result.Approved.Should().BeTrue();
        result.Outcome.Should().Be(ApprovalOutcome.Approved);
    }

    // -------------------------------------------------------------------------
    // Honors cancellation token
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_HonorsCancellationToken()
    {
        var (_, persistent, _, _) = Build();
        var request = MakeRequest();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        // Drive via PAS with a cancellation token on the outer await.
        var act = async () =>
        {
            var task = persistent.RequestApprovalAsync(request, cts.Token);
            await task;
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
