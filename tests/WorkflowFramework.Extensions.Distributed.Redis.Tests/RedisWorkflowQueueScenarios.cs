using FluentAssertions;
using NSubstitute;
using StackExchange.Redis;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Distributed.Redis.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Distributed.Redis.Tests;

/// <summary>
/// Characterization tests for RedisWorkflowQueue using a faked IDatabase.
/// </summary>
[Feature("RedisWorkflowQueue")]
public class RedisWorkflowQueueScenarios : RedisTestBase
{
    public RedisWorkflowQueueScenarios(ITestOutputHelper output) : base(output) { }

    // --- helpers ---

    private static (IDatabase db, RedisWorkflowQueue queue) MakeQueue(string queueKey = "workflow:queue")
    {
        var db = Substitute.For<IDatabase>();
        db.ListRightPushAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(1L));
        db.ListLeftPopAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(RedisValue.Null));
        db.ListLengthAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(0L));
        var queue = new RedisWorkflowQueue(db, queueKey);
        return (db, queue);
    }

    // --- scenarios ---

    [Scenario("Constructor throws ArgumentNullException when database is null"), Fact]
    public async Task ConstructorRequiresDatabase()
    {
        Exception? caught = null;
        try { _ = new RedisWorkflowQueue(null!); }
        catch (Exception ex) { caught = ex; }

        await Given("a null IDatabase", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("EnqueueAsync calls ListRightPushAsync with serialized item"), Fact]
    public async Task EnqueueCallsListRightPush()
    {
        var (db, queue) = MakeQueue();
        var item = new WorkflowQueueItem { WorkflowName = "wf-1" };

        await queue.EnqueueAsync(item);

        await Given("an enqueued WorkflowQueueItem", () => db)
            .Then("ListRightPushAsync is called with JSON-serialized payload", fakeDb =>
            {
                fakeDb.Received(1).ListRightPushAsync(
                    Arg.Any<RedisKey>(),
                    Arg.Is<RedisValue>(v => ((string)v!).Contains("wf-1")),
                    Arg.Any<When>(),
                    Arg.Any<CommandFlags>());
                return true;
            })
            .AssertPassed();
    }

    [Scenario("DequeueAsync returns null when the queue is empty"), Fact]
    public async Task DequeueReturnsNullOnEmptyQueue()
    {
        var (db, queue) = MakeQueue();
        // Default stub returns RedisValue.Null (empty)

        var item = await queue.DequeueAsync();

        await Given("a DequeueAsync on an empty queue (Redis returns null)", () => item)
            .Then("DequeueAsync returns null", i =>
            {
                i.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("DequeueAsync deserializes returned JSON into WorkflowQueueItem"), Fact]
    public async Task DequeueDeserializesItem()
    {
        var (db, queue) = MakeQueue();
        var original = new WorkflowQueueItem { WorkflowName = "wf-deserialize", Id = "abc123" };
        var json = System.Text.Json.JsonSerializer.Serialize(original);

        db.ListLeftPopAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(new RedisValue(json)));

        var item = await queue.DequeueAsync();

        await Given("a queue that returns a JSON WorkflowQueueItem from Redis", () => item)
            .Then("the item is deserialized with correct WorkflowName and Id", i =>
            {
                i.Should().NotBeNull();
                i!.WorkflowName.Should().Be("wf-deserialize");
                i.Id.Should().Be("abc123");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("GetLengthAsync returns the current queue length from Redis"), Fact]
    public async Task GetLengthReturnsRedisLength()
    {
        var (db, queue) = MakeQueue();
        db.ListLengthAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(7L));

        var length = await queue.GetLengthAsync();

        await Given("a queue where Redis reports length 7", () => length)
            .Then("GetLengthAsync returns 7", l =>
            {
                l.Should().Be(7);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Queue uses the default key 'workflow:queue' when none is specified"), Fact]
    public async Task DefaultQueueKey()
    {
        var db = Substitute.For<IDatabase>();
        db.ListRightPushAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(1L));
        var queue = new RedisWorkflowQueue(db); // no key arg = default

        await queue.EnqueueAsync(new WorkflowQueueItem { WorkflowName = "test" });

        await Given("a RedisWorkflowQueue created with no explicit queue key", () => db)
            .Then("ListRightPushAsync is called with the default key 'workflow:queue'", fakeDb =>
            {
                fakeDb.Received(1).ListRightPushAsync(
                    Arg.Is<RedisKey>(k => ((string)k!).Contains("workflow:queue")),
                    Arg.Any<RedisValue>(),
                    Arg.Any<When>(),
                    Arg.Any<CommandFlags>());
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Queue uses a custom key when specified in constructor"), Fact]
    public async Task CustomQueueKey()
    {
        var (db, queue) = MakeQueue("my-custom-queue");

        await queue.EnqueueAsync(new WorkflowQueueItem { WorkflowName = "wf" });

        await Given("a RedisWorkflowQueue created with key 'my-custom-queue'", () => db)
            .Then("ListRightPushAsync is called with 'my-custom-queue'", fakeDb =>
            {
                fakeDb.Received(1).ListRightPushAsync(
                    Arg.Is<RedisKey>(k => ((string)k!).Contains("my-custom-queue")),
                    Arg.Any<RedisValue>(),
                    Arg.Any<When>(),
                    Arg.Any<CommandFlags>());
                return true;
            })
            .AssertPassed();
    }
}
