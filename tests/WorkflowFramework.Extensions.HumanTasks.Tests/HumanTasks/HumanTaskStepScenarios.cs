using FluentAssertions;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.HumanTasks;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.HumanTasks.Tests.HumanTasks;

[Feature("HumanTaskStep — workflow step that pauses for human input")]
public class HumanTaskStepScenarios : TinyBddXunitBase
{
    public HumanTaskStepScenarios(ITestOutputHelper output) : base(output) { }

    private static ITaskInbox MakeAutoCompleteInbox(string outcome = "approved")
    {
        var inbox = Substitute.For<ITaskInbox>();
        var created = new HumanTask { Title = "Review", Assignee = "alice" };
        inbox.CreateTaskAsync(Arg.Any<HumanTask>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var t = ci.ArgAt<HumanTask>(0);
                t.Outcome = outcome;
                return Task.FromResult(t);
            });
        inbox.WaitForCompletionAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var completedTask = new HumanTask { Id = ci.ArgAt<string>(0), Outcome = outcome, Status = HumanTaskStatus.Approved };
                return Task.FromResult(completedTask);
            });
        return inbox;
    }

    [Scenario("Default step name uses task title"), Fact]
    public async Task DefaultStepName_UsesTitleInName()
    {
        var options = new HumanTaskOptions { Title = "Review Document", Assignee = "alice", Timeout = TimeSpan.FromMinutes(1) };
        var step = new HumanTaskStep(MakeAutoCompleteInbox(), options);

        await Given("HumanTaskStep with Title='Review Document'", () => step.Name)
            .Then("Name contains the title", name =>
            {
                name.Should().Contain("Review Document");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Custom step name from options is used"), Fact]
    public async Task CustomStepName_FromOptions()
    {
        var options = new HumanTaskOptions { StepName = "MyHumanTask", Title = "T", Assignee = "bob", Timeout = TimeSpan.FromMinutes(1) };
        var step = new HumanTaskStep(MakeAutoCompleteInbox(), options);

        await Given("HumanTaskStep with StepName='MyHumanTask'", () => step.Name)
            .Then("Name is 'MyHumanTask'", name =>
            {
                name.Should().Be("MyHumanTask");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync stores task ID in context properties"), Fact]
    public async Task ExecuteAsync_StoresTaskId()
    {
        var inbox = new InMemoryTaskInbox();
        var options = new HumanTaskOptions { Title = "Sign Off", Assignee = "alice", Timeout = TimeSpan.FromSeconds(5) };
        var step = new HumanTaskStep(inbox, options);
        var context = new WorkflowContext();

        // Complete the task concurrently so Wait resolves
        _ = Task.Run(async () =>
        {
            await Task.Delay(30);
            var tasks = await inbox.GetTasksForAssigneeAsync("alice");
            if (tasks.Count > 0) await inbox.CompleteTaskAsync(tasks[0].Id, "approved");
        });

        await step.ExecuteAsync(context);

        await Given("HumanTaskStep executed with InMemoryTaskInbox", () => context.Properties)
            .Then("context contains HumanTask(Sign Off).TaskId", props =>
            {
                props.Keys.Should().Contain(k => k.Contains("TaskId"));
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync stores outcome in context properties"), Fact]
    public async Task ExecuteAsync_StoresOutcome()
    {
        var inbox = MakeAutoCompleteInbox("approved");
        var options = new HumanTaskOptions { Title = "Review", Assignee = "alice", Timeout = TimeSpan.FromSeconds(1) };
        var step = new HumanTaskStep(inbox, options);
        var context = new WorkflowContext();

        await step.ExecuteAsync(context);

        await Given("HumanTaskStep completed with 'approved' outcome", () => context.Properties)
            .Then("HumanTask(Review).Outcome is stored", props =>
            {
                props.Keys.Should().Contain(k => k.Contains("Outcome"));
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null inbox throws ArgumentNullException"), Fact]
    public async Task NullInbox_Throws()
    {
        Exception? caught = null;
        try { _ = new HumanTaskStep(null!, new HumanTaskOptions()); }
        catch (Exception ex) { caught = ex; }

        await Given("null inbox passed to constructor", () => caught)
            .Then("ArgumentNullException thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null options throws ArgumentNullException"), Fact]
    public async Task NullOptions_Throws()
    {
        var inbox = MakeAutoCompleteInbox();
        Exception? caught = null;
        try { _ = new HumanTaskStep(inbox, null!); }
        catch (Exception ex) { caught = ex; }

        await Given("null options passed to constructor", () => caught)
            .Then("ArgumentNullException thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }
}
