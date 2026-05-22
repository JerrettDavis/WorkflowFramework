using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Scheduling;

namespace WorkflowFramework.Tests.TinyBDD.Scheduling;

[Feature("InMemory approval service")]
public class InMemoryApprovalServiceTests : TinyBddTestBase
{
    public InMemoryApprovalServiceTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Approve resolves the pending request to true"), Fact]
    public async Task ApproveResolvesToTrue()
    {
        var svc = new InMemoryApprovalService();
        var config = new ApprovalConfig { Name = "test-approval" };
        var approvalTask = svc.RequestApprovalAsync("wf-approve", config);
        svc.Approve("wf-approve");
        var result = await approvalTask;

        await Given("the approval result after calling Approve", () => result)
            .Then("the result is true", r =>
            {
                r.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Reject resolves the pending request to false"), Fact]
    public async Task RejectResolvesToFalse()
    {
        var svc = new InMemoryApprovalService();
        var config = new ApprovalConfig { Name = "test-approval" };
        var approvalTask = svc.RequestApprovalAsync("wf-reject", config);
        svc.Reject("wf-reject");
        var result = await approvalTask;

        await Given("the approval result after calling Reject", () => result)
            .Then("the result is false", r =>
            {
                r.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Timeout causes the pending request to resolve to false"), Fact]
    public async Task TimeoutResolvesToFalse()
    {
        var svc = new InMemoryApprovalService();
        var config = new ApprovalConfig { Timeout = TimeSpan.FromMilliseconds(100) };
        var result = await svc.RequestApprovalAsync("wf-timeout", config);

        await Given("the result of an approval request with a 100ms timeout that was not actioned", () => result)
            .Then("the result is false", r =>
            {
                r.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Cancellation via token causes OperationCanceledException"), Fact]
    public async Task CancellationThrows()
    {
        var svc = new InMemoryApprovalService();
        var config = new ApprovalConfig { Name = "cancel-test" };
        using var cts = new CancellationTokenSource();
        var approvalTask = svc.RequestApprovalAsync("wf-cancel", config, cts.Token);
        cts.Cancel();

        Exception? caught = null;
        try { await approvalTask; }
        catch (OperationCanceledException ex) { caught = ex; }

        await Given("the caught exception after cancelling the approval token", () => caught)
            .Then("an OperationCanceledException was thrown", ex =>
            {
                ex.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }
}
