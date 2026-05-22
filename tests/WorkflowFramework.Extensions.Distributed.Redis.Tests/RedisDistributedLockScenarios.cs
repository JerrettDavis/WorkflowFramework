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
/// Characterization tests for RedisDistributedLock using a faked IDatabase.
/// Docker-dependent scenarios are skipped unless REDIS_TESTS_DOCKER=1 is set.
/// </summary>
[Feature("RedisDistributedLock")]
public class RedisDistributedLockScenarios : RedisTestBase
{
    public RedisDistributedLockScenarios(ITestOutputHelper output) : base(output) { }

    // --- helpers ---

    private static IDatabase MakeFakeDb(bool acquireSucceeds = true)
    {
        var db = Substitute.For<IDatabase>();
        // Stub the 4-arg overload: (RedisKey, RedisValue, TimeSpan?, When)
        // which is what RedisDistributedLock uses with When.NotExists
        db.StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<When>())
            .Returns(Task.FromResult(acquireSucceeds));
        // Also stub the 5-arg overload just in case
        db.StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<When>(),
                Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(acquireSucceeds));
        db.ScriptEvaluateAsync(
                Arg.Any<string>(),
                Arg.Any<RedisKey[]?>(),
                Arg.Any<RedisValue[]?>(),
                Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(RedisResult.Create(1L)));
        return db;
    }

    // --- scenarios ---

    [Scenario("Constructor throws ArgumentNullException when database is null"), Fact]
    public async Task ConstructorRequiresDatabase()
    {
        Exception? caught = null;
        try { _ = new RedisDistributedLock(null!); }
        catch (Exception ex) { caught = ex; }

        await Given("a null IDatabase", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AcquireAsync returns a non-null handle when lock is available"), Fact]
    public async Task AcquireSucceedsWhenLockAvailable()
    {
        var db = MakeFakeDb(acquireSucceeds: true);
        var distributedLock = new RedisDistributedLock(db);

        var handle = await distributedLock.AcquireAsync("my-key", TimeSpan.FromSeconds(10));

        await Given("a Redis lock where StringSetAsync returns true", () => handle)
            .Then("AcquireAsync returns a non-null handle", h =>
            {
                h.Should().NotBeNull();
                return true;
            })
            .AssertPassed();

        if (handle is not null)
            await handle.DisposeAsync();
    }

    [Scenario("AcquireAsync returns null when lock is already held"), Fact]
    public async Task AcquireReturnsNullWhenLockHeld()
    {
        var db = MakeFakeDb(acquireSucceeds: false);
        var distributedLock = new RedisDistributedLock(db);

        var handle = await distributedLock.AcquireAsync("held-key", TimeSpan.FromSeconds(10));

        await Given("a Redis lock where StringSetAsync returns false (key exists)", () => handle)
            .Then("AcquireAsync returns null", h =>
            {
                h.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Disposing the lock handle executes the Lua release script"), Fact]
    public async Task DisposingHandleReleasesLock()
    {
        var db = MakeFakeDb(acquireSucceeds: true);
        var distributedLock = new RedisDistributedLock(db);
        var handle = await distributedLock.AcquireAsync("release-key", TimeSpan.FromSeconds(10));
        handle.Should().NotBeNull();

        await handle!.DisposeAsync();

        await Given("a lock handle that was acquired and then disposed", () => db)
            .Then("ScriptEvaluateAsync was called to release the lock", fakeDb =>
            {
                fakeDb.Received(1).ScriptEvaluateAsync(
                    Arg.Any<string>(),
                    Arg.Any<RedisKey[]?>(),
                    Arg.Any<RedisValue[]?>(),
                    Arg.Any<CommandFlags>());
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Disposing the lock handle a second time is idempotent"), Fact]
    public async Task DoubleDisposeIsIdempotent()
    {
        var db = MakeFakeDb(acquireSucceeds: true);
        var distributedLock = new RedisDistributedLock(db);
        var handle = await distributedLock.AcquireAsync("idempotent-key", TimeSpan.FromSeconds(10));
        handle.Should().NotBeNull();

        await handle!.DisposeAsync();
        await handle.DisposeAsync(); // second dispose — should not call release again

        await Given("a lock handle disposed twice", () => db)
            .Then("ScriptEvaluateAsync was called exactly once (not twice)", fakeDb =>
            {
                fakeDb.Received(1).ScriptEvaluateAsync(
                    Arg.Any<string>(),
                    Arg.Any<RedisKey[]?>(),
                    Arg.Any<RedisValue[]?>(),
                    Arg.Any<CommandFlags>());
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Lock key is prefixed with 'workflow:lock:' in Redis"), Fact]
    public async Task LockKeyIsPrefixed()
    {
        var db = MakeFakeDb(acquireSucceeds: true);
        var distributedLock = new RedisDistributedLock(db);
        const string logicalKey = "my-workflow-lock";

        var handle = await distributedLock.AcquireAsync(logicalKey, TimeSpan.FromSeconds(5));
        if (handle is not null) await handle.DisposeAsync();

        await Given("AcquireAsync called with key 'my-workflow-lock'", () => db)
            .Then("the Redis StringSetAsync call uses the prefixed key 'workflow:lock:my-workflow-lock'", fakeDb =>
            {
                // RedisDistributedLock calls the 4-arg overload (RedisKey, RedisValue, TimeSpan?, When)
                fakeDb.Received().StringSetAsync(
                    Arg.Is<RedisKey>(k => ((string)k!).Contains("workflow:lock:my-workflow-lock")),
                    Arg.Any<RedisValue>(),
                    Arg.Any<TimeSpan?>(),
                    Arg.Any<When>());
                return true;
            })
            .AssertPassed();
    }
}
