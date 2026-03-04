using FluentAssertions;
using WorkflowFramework.Extensions.HumanTasks;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.HumanTasks;

public class ApprovalStepTests
{
    [Fact]
    public void Constructor_NullInbox_Throws()
    {
        FluentActions.Invoking(() => new ApprovalStep(null!, new ApprovalOptions()))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        FluentActions.Invoking(() => new ApprovalStep(new InMemoryTaskInbox(), null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_Default()
    {
        var step = new ApprovalStep(new InMemoryTaskInbox(), new ApprovalOptions());
        step.Name.Should().Be("Approval");
    }

    [Fact]
    public void Name_Custom()
    {
        var step = new ApprovalStep(new InMemoryTaskInbox(), new ApprovalOptions { StepName = "Custom" });
        step.Name.Should().Be("Custom");
    }

    [Fact]
    public async Task Sequential_AllApproved()
    {
        var inbox = new InMemoryTaskInbox();
        var step = new ApprovalStep(inbox, new ApprovalOptions
        {
            Title = "Approve",
            Approvers = new List<string> { "alice", "bob" },
            Mode = ApprovalMode.Sequential,
            Timeout = TimeSpan.FromSeconds(5)
        });
        var ctx = CreateContext();
        var execTask = step.ExecuteAsync(ctx);

        // Complete alice's task
        var aliceTask = await WaitForLatestTaskAsync(inbox, "alice");
        await inbox.CompleteTaskAsync(aliceTask.Id, "approved");

        // Complete bob's task
        var bobTask = await WaitForLatestTaskAsync(inbox, "bob");
        await inbox.CompleteTaskAsync(bobTask.Id, "approved");

        await execTask;
        ctx.Properties["Approval.Approved"].Should().Be(true);
    }

    [Fact]
    public async Task Sequential_Rejection_StopsChain()
    {
        var inbox = new InMemoryTaskInbox();
        var step = new ApprovalStep(inbox, new ApprovalOptions
        {
            Title = "Approve",
            Approvers = new List<string> { "alice", "bob" },
            Mode = ApprovalMode.Sequential,
            Timeout = TimeSpan.FromSeconds(5)
        });
        var ctx = CreateContext();
        var execTask = step.ExecuteAsync(ctx);

        var aliceTask = await WaitForLatestTaskAsync(inbox, "alice");
        await inbox.CompleteTaskAsync(aliceTask.Id, "rejected");

        await execTask;
        ctx.Properties["Approval.Approved"].Should().Be(false);
        // Bob should never have been asked
        var bobTasks = await inbox.GetTasksForAssigneeAsync("bob");
        bobTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task Parallel_AllApproved()
    {
        var inbox = new InMemoryTaskInbox();
        var step = new ApprovalStep(inbox, new ApprovalOptions
        {
            Title = "Approve",
            Approvers = new List<string> { "alice", "bob" },
            Mode = ApprovalMode.Parallel,
            Timeout = TimeSpan.FromSeconds(5)
        });
        var ctx = CreateContext();
        var execTask = step.ExecuteAsync(ctx);

        var aliceTask = await WaitForLatestTaskAsync(inbox, "alice");
        var bobTask = await WaitForLatestTaskAsync(inbox, "bob");
        await inbox.CompleteTaskAsync(aliceTask.Id, "approved");
        await inbox.CompleteTaskAsync(bobTask.Id, "approved");

        await execTask;
        ctx.Properties["Approval.Approved"].Should().Be(true);
    }

    [Fact]
    public async Task Parallel_OneRejected()
    {
        var inbox = new InMemoryTaskInbox();
        var step = new ApprovalStep(inbox, new ApprovalOptions
        {
            Title = "Approve",
            Approvers = new List<string> { "alice", "bob" },
            Mode = ApprovalMode.Parallel,
            Timeout = TimeSpan.FromSeconds(5)
        });
        var ctx = CreateContext();
        var execTask = step.ExecuteAsync(ctx);

        var aliceTask = await WaitForLatestTaskAsync(inbox, "alice");
        var bobTask = await WaitForLatestTaskAsync(inbox, "bob");
        await inbox.CompleteTaskAsync(aliceTask.Id, "approved");
        await inbox.CompleteTaskAsync(bobTask.Id, "rejected");

        await execTask;
        ctx.Properties["Approval.Approved"].Should().Be(false);
    }

    [Fact]
    public void ApprovalOptions_Defaults()
    {
        var opts = new ApprovalOptions();
        opts.Mode.Should().Be(ApprovalMode.Sequential);
        opts.Timeout.Should().Be(TimeSpan.FromHours(24));
        opts.Approvers.Should().BeEmpty();
        opts.Title.Should().BeEmpty();
    }

    [Fact]
    public void ApprovalMode_Values()
    {
        Enum.GetValues<ApprovalMode>().Should().HaveCount(2);
    }

    private static async Task<HumanTask> WaitForLatestTaskAsync(InMemoryTaskInbox inbox, string assignee, int timeoutMs = 2_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var tasks = await inbox.GetTasksForAssigneeAsync(assignee);
            if (tasks.Count > 0)
                return tasks[0];

            await Task.Delay(20);
        }

        var finalTasks = await inbox.GetTasksForAssigneeAsync(assignee);
        finalTasks.Should().NotBeEmpty($"task for approver '{assignee}' should be created before completion");
        return finalTasks[0];
    }

    private static IWorkflowContext CreateContext() => new Ctx();

    private class Ctx : IWorkflowContext
    {
        public string WorkflowId { get; set; } = "wf1";
        public string CorrelationId { get; set; } = "c1";
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; }
        public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; }
        public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}
