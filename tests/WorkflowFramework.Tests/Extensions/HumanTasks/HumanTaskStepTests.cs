using FluentAssertions;
using WorkflowFramework.Extensions.HumanTasks;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.HumanTasks;

public class HumanTaskStepTests
{
    [Fact]
    public void Constructor_NullInbox_Throws()
    {
        FluentActions.Invoking(() => new HumanTaskStep(null!, new HumanTaskOptions()))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        FluentActions.Invoking(() => new HumanTaskStep(new InMemoryTaskInbox(), null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_UsesStepNameFromOptions()
    {
        var step = new HumanTaskStep(new InMemoryTaskInbox(), new HumanTaskOptions { StepName = "MyStep" });
        step.Name.Should().Be("MyStep");
    }

    [Fact]
    public void Name_FallbackToTitle()
    {
        var step = new HumanTaskStep(new InMemoryTaskInbox(), new HumanTaskOptions { Title = "Review" });
        step.Name.Should().Be("HumanTask(Review)");
    }

    [Fact]
    public async Task ExecuteAsync_CreatesTaskAndWaitsForCompletion()
    {
        var inbox = new InMemoryTaskInbox();
        var step = new HumanTaskStep(inbox, new HumanTaskOptions
        {
            Title = "Approve",
            Assignee = "alice",
            Timeout = TimeSpan.FromSeconds(5)
        });
        var ctx = new TestCtx();
        var execTask = step.ExecuteAsync(ctx);
        await Task.Delay(50);

        // Complete the task that was created
        var tasks = await inbox.GetTasksForAssigneeAsync("alice");
        tasks.Should().HaveCount(1);
        await inbox.CompleteTaskAsync(tasks[0].Id, "approved");

        await execTask;
        ctx.Properties.Should().ContainKey("HumanTask(Approve).TaskId");
        ctx.Properties["HumanTask(Approve).Outcome"].Should().Be("approved");
        ctx.Properties["HumanTask(Approve).Status"].Should().Be("Approved");
    }

    [Fact]
    public void HumanTaskOptions_Defaults()
    {
        var opts = new HumanTaskOptions();
        opts.StepName.Should().BeNull();
        opts.Timeout.Should().Be(TimeSpan.FromHours(24));
        opts.Escalation.Should().BeNull();
    }

    private class TestCtx : IWorkflowContext
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

public class HumanTaskModelTests
{
    [Fact]
    public void HumanTask_Defaults()
    {
        var t = new HumanTask();
        t.Id.Should().NotBeNullOrEmpty();
        t.Status.Should().Be(HumanTaskStatus.Pending);
        t.WorkflowId.Should().BeEmpty();
        t.Title.Should().BeEmpty();
        t.Assignee.Should().BeEmpty();
        t.DueDate.Should().BeNull();
        t.Outcome.Should().BeNull();
        t.Escalation.Should().BeNull();
        t.DelegatedTo.Should().BeNull();
        t.Data.Should().BeEmpty();
    }

    [Fact]
    public void EscalationRule_Properties()
    {
        var r = new EscalationRule
        {
            Timeout = TimeSpan.FromHours(2),
            EscalateTo = "mgr"
        };
        r.Timeout.Should().Be(TimeSpan.FromHours(2));
        r.EscalateTo.Should().Be("mgr");
        r.OnEscalation.Should().BeNull();
    }

    [Fact]
    public void HumanTaskStatus_AllValues()
    {
        Enum.GetValues<HumanTaskStatus>().Should().HaveCount(7);
    }
}
