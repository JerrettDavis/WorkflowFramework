using FluentAssertions;
using WorkflowFramework.Extensions.HumanTasks;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.HumanTasks;

public class InMemoryTaskInboxTests
{
    private readonly InMemoryTaskInbox _inbox = new();

    [Fact]
    public async Task CreateTaskAsync_NullTask_Throws()
    {
        await _inbox.Invoking(i => i.CreateTaskAsync(null!)).Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateTaskAsync_ReturnsSameTask()
    {
        var task = new HumanTask { Title = "Test" };
        var result = await _inbox.CreateTaskAsync(task);
        result.Should().BeSameAs(task);
    }

    [Fact]
    public async Task GetTaskAsync_Existing_ReturnsTask()
    {
        var task = new HumanTask();
        await _inbox.CreateTaskAsync(task);
        var result = await _inbox.GetTaskAsync(task.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(task.Id);
    }

    [Fact]
    public async Task GetTaskAsync_NonExistent_ReturnsNull()
    {
        var result = await _inbox.GetTaskAsync("nope");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTasksForAssigneeAsync_FiltersCorrectly()
    {
        await _inbox.CreateTaskAsync(new HumanTask { Assignee = "alice" });
        await _inbox.CreateTaskAsync(new HumanTask { Assignee = "bob" });
        await _inbox.CreateTaskAsync(new HumanTask { Assignee = "alice" });
        var tasks = await _inbox.GetTasksForAssigneeAsync("alice");
        tasks.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTasksForAssigneeAsync_NoMatch_ReturnsEmpty()
    {
        var tasks = await _inbox.GetTasksForAssigneeAsync("nobody");
        tasks.Should().BeEmpty();
    }

    [Theory]
    [InlineData("approved", HumanTaskStatus.Approved)]
    [InlineData("rejected", HumanTaskStatus.Rejected)]
    [InlineData("done", HumanTaskStatus.Completed)]
    public async Task CompleteTaskAsync_SetsCorrectStatus(string outcome, HumanTaskStatus expected)
    {
        var task = new HumanTask();
        await _inbox.CreateTaskAsync(task);
        await _inbox.CompleteTaskAsync(task.Id, outcome);
        var result = await _inbox.GetTaskAsync(task.Id);
        result!.Status.Should().Be(expected);
        result.Outcome.Should().Be(outcome);
    }

    [Fact]
    public async Task CompleteTaskAsync_WithData_MergesData()
    {
        var task = new HumanTask();
        await _inbox.CreateTaskAsync(task);
        await _inbox.CompleteTaskAsync(task.Id, "done", new Dictionary<string, object?> { ["key"] = "val" });
        var result = await _inbox.GetTaskAsync(task.Id);
        result!.Data["key"].Should().Be("val");
    }

    [Fact]
    public async Task CompleteTaskAsync_NonExistent_DoesNotThrow()
    {
        await _inbox.Invoking(i => i.CompleteTaskAsync("nope", "done")).Should().NotThrowAsync();
    }

    [Fact]
    public async Task DelegateTaskAsync_UpdatesAssignee()
    {
        var task = new HumanTask { Assignee = "alice" };
        await _inbox.CreateTaskAsync(task);
        await _inbox.DelegateTaskAsync(task.Id, "bob");
        var result = await _inbox.GetTaskAsync(task.Id);
        result!.Assignee.Should().Be("bob");
        result.DelegatedTo.Should().Be("bob");
    }

    [Fact]
    public async Task DelegateTaskAsync_NonExistent_DoesNotThrow()
    {
        await _inbox.Invoking(i => i.DelegateTaskAsync("nope", "bob")).Should().NotThrowAsync();
    }

    [Fact]
    public async Task WaitForCompletionAsync_AlreadyCompleted_ReturnsImmediately()
    {
        var task = new HumanTask { Status = HumanTaskStatus.Completed };
        await _inbox.CreateTaskAsync(task);
        var result = await _inbox.WaitForCompletionAsync(task.Id, TimeSpan.FromSeconds(1));
        result.Status.Should().Be(HumanTaskStatus.Completed);
    }

    [Fact]
    public async Task WaitForCompletionAsync_CompletedLater_Resolves()
    {
        var task = new HumanTask();
        await _inbox.CreateTaskAsync(task);
        var waitTask = _inbox.WaitForCompletionAsync(task.Id, TimeSpan.FromSeconds(5));
        await Task.Delay(30);
        await _inbox.CompleteTaskAsync(task.Id, "approved");
        var result = await waitTask;
        result.Status.Should().Be(HumanTaskStatus.Approved);
    }

    [Fact]
    public async Task WaitForCompletionAsync_Timeout_ThrowsCancellation()
    {
        var task = new HumanTask();
        await _inbox.CreateTaskAsync(task);
        await _inbox.Invoking(i => i.WaitForCompletionAsync(task.Id, TimeSpan.FromMilliseconds(30)))
            .Should().ThrowAsync<TaskCanceledException>();
    }
}
