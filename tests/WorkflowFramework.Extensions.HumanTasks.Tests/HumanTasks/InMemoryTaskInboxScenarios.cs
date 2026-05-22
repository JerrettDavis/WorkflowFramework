using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.HumanTasks;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.HumanTasks.Tests.HumanTasks;

[Feature("InMemoryTaskInbox — in-memory human task store")]
public class InMemoryTaskInboxScenarios : TinyBddXunitBase
{
    public InMemoryTaskInboxScenarios(ITestOutputHelper output) : base(output) { }

    private static HumanTask MakeTask(string assignee = "alice", string title = "Review") =>
        new HumanTask { Assignee = assignee, Title = title, WorkflowId = "wf-1" };

    [Scenario("CreateTaskAsync stores and returns the task"), Fact]
    public async Task CreateTask_StoresTask()
    {
        var inbox = new InMemoryTaskInbox();
        var task = MakeTask();
        var created = await inbox.CreateTaskAsync(task);

        await Given("a new task created", () => created)
            .Then("returned task is the same instance", t =>
            {
                t.Should().BeSameAs(task);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("GetTaskAsync returns the stored task"), Fact]
    public async Task GetTask_ReturnsStoredTask()
    {
        var inbox = new InMemoryTaskInbox();
        var task = MakeTask();
        await inbox.CreateTaskAsync(task);

        var found = await inbox.GetTaskAsync(task.Id);

        await Given("task created then retrieved by id", () => found)
            .Then("task is not null and has same id", t =>
            {
                t.Should().NotBeNull();
                t!.Id.Should().Be(task.Id);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("GetTaskAsync returns null for unknown id"), Fact]
    public async Task GetTask_UnknownId_ReturnsNull()
    {
        var inbox = new InMemoryTaskInbox();
        var result = await inbox.GetTaskAsync("no-such-id");

        await Given("inbox with no tasks", () => result)
            .Then("null is returned", r =>
            {
                r.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("GetTasksForAssigneeAsync returns only tasks for that assignee"), Fact]
    public async Task GetTasksForAssignee_ReturnsCorrectTasks()
    {
        var inbox = new InMemoryTaskInbox();
        await inbox.CreateTaskAsync(MakeTask("alice", "T1"));
        await inbox.CreateTaskAsync(MakeTask("alice", "T2"));
        await inbox.CreateTaskAsync(MakeTask("bob", "T3"));

        var aliceTasks = await inbox.GetTasksForAssigneeAsync("alice");

        await Given("two alice tasks and one bob task", () => aliceTasks)
            .Then("alice gets exactly 2 tasks", tasks =>
            {
                tasks.Should().HaveCount(2);
                tasks.Should().OnlyContain(t => t.Assignee == "alice");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CompleteTaskAsync with 'approved' sets status to Approved"), Fact]
    public async Task CompleteTask_Approved_SetsStatus()
    {
        var inbox = new InMemoryTaskInbox();
        var task = MakeTask();
        await inbox.CreateTaskAsync(task);

        await inbox.CompleteTaskAsync(task.Id, "approved");

        await Given("task completed with outcome 'approved'", () => task)
            .Then("task.Status is Approved", t =>
            {
                t.Status.Should().Be(HumanTaskStatus.Approved);
                t.Outcome.Should().Be("approved");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CompleteTaskAsync with 'rejected' sets status to Rejected"), Fact]
    public async Task CompleteTask_Rejected_SetsStatus()
    {
        var inbox = new InMemoryTaskInbox();
        var task = MakeTask();
        await inbox.CreateTaskAsync(task);

        await inbox.CompleteTaskAsync(task.Id, "rejected");

        await Given("task completed with outcome 'rejected'", () => task)
            .Then("task.Status is Rejected", t =>
            {
                t.Status.Should().Be(HumanTaskStatus.Rejected);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CompleteTaskAsync with custom outcome sets status to Completed"), Fact]
    public async Task CompleteTask_CustomOutcome_SetsCompleted()
    {
        var inbox = new InMemoryTaskInbox();
        var task = MakeTask();
        await inbox.CreateTaskAsync(task);

        await inbox.CompleteTaskAsync(task.Id, "skipped");

        await Given("task completed with custom outcome 'skipped'", () => task)
            .Then("task.Status is Completed and outcome is 'skipped'", t =>
            {
                t.Status.Should().Be(HumanTaskStatus.Completed);
                t.Outcome.Should().Be("skipped");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CompleteTaskAsync merges extra data into task"), Fact]
    public async Task CompleteTask_MergesData()
    {
        var inbox = new InMemoryTaskInbox();
        var task = MakeTask();
        await inbox.CreateTaskAsync(task);

        await inbox.CompleteTaskAsync(task.Id, "approved",
            new Dictionary<string, object?> { ["comment"] = "LGTM" });

        await Given("complete call with extra data", () => task.Data)
            .Then("data contains the extra key", d =>
            {
                d.Should().ContainKey("comment");
                d["comment"].Should().Be("LGTM");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("DelegateTaskAsync sets DelegatedTo on the task"), Fact]
    public async Task DelegateTask_SetsDelegatedTo()
    {
        var inbox = new InMemoryTaskInbox();
        var task = MakeTask("alice");
        await inbox.CreateTaskAsync(task);

        await inbox.DelegateTaskAsync(task.Id, "carol");

        await Given("task delegated to carol", () => task.DelegatedTo)
            .Then("DelegatedTo is 'carol'", dt =>
            {
                dt.Should().Be("carol");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CreateTaskAsync with null task throws ArgumentNullException"), Fact]
    public async Task CreateTask_NullTask_Throws()
    {
        var inbox = new InMemoryTaskInbox();
        Exception? caught = null;
        try { await inbox.CreateTaskAsync(null!); }
        catch (Exception ex) { caught = ex; }

        await Given("null task passed to CreateTaskAsync", () => caught)
            .Then("ArgumentNullException thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WaitForCompletionAsync resolves when task is completed concurrently"), Fact]
    public async Task WaitForCompletion_ResolvesOnCompletion()
    {
        var inbox = new InMemoryTaskInbox();
        var task = MakeTask();
        await inbox.CreateTaskAsync(task);

        // Fire off completion after a tiny delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            await inbox.CompleteTaskAsync(task.Id, "done");
        });

        var completed = await inbox.WaitForCompletionAsync(task.Id, TimeSpan.FromSeconds(5));

        await Given("task completed concurrently during wait", () => completed)
            .Then("WaitForCompletion returns the completed task", t =>
            {
                t.Should().NotBeNull();
                t.Outcome.Should().Be("done");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WaitForCompletionAsync times out when task is not completed"), Fact]
    public async Task WaitForCompletion_TimesOut()
    {
        var inbox = new InMemoryTaskInbox();
        var task = MakeTask();
        await inbox.CreateTaskAsync(task);

        Exception? caught = null;
        try
        {
            await inbox.WaitForCompletionAsync(task.Id, TimeSpan.FromMilliseconds(50));
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        await Given("task not completed within timeout", () => caught)
            .Then("a TimeoutException or OperationCanceledException is thrown", ex =>
            {
                ex.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }
}
