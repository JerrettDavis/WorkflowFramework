using FluentAssertions;
using WorkflowFramework.Extensions.Distributed;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Distributed;

public class DistributedLockTests
{
    private readonly InMemoryDistributedLock _lock = new();

    [Fact]
    public async Task AcquireAsync_ReturnsHandle()
    {
        var handle = await _lock.AcquireAsync("key1", TimeSpan.FromSeconds(30));
        handle.Should().NotBeNull();
        await handle!.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_SameKey_Contention_ReturnsNull()
    {
        var handle1 = await _lock.AcquireAsync("key1", TimeSpan.FromSeconds(30));
        handle1.Should().NotBeNull();
        var handle2 = await _lock.AcquireAsync("key1", TimeSpan.FromSeconds(30));
        handle2.Should().BeNull();
        await handle1!.DisposeAsync();
    }

    [Fact]
    public async Task Release_AllowsReacquisition()
    {
        var handle1 = await _lock.AcquireAsync("key1", TimeSpan.FromSeconds(30));
        await handle1!.DisposeAsync();
        var handle2 = await _lock.AcquireAsync("key1", TimeSpan.FromSeconds(30));
        handle2.Should().NotBeNull();
        await handle2!.DisposeAsync();
    }

    [Fact]
    public async Task DifferentKeys_IndependentLocks()
    {
        var h1 = await _lock.AcquireAsync("a", TimeSpan.FromSeconds(30));
        var h2 = await _lock.AcquireAsync("b", TimeSpan.FromSeconds(30));
        h1.Should().NotBeNull();
        h2.Should().NotBeNull();
        await h1!.DisposeAsync();
        await h2!.DisposeAsync();
    }

    [Fact]
    public async Task DoubleDispose_DoesNotThrow()
    {
        var handle = await _lock.AcquireAsync("key", TimeSpan.FromSeconds(30));
        await handle!.DisposeAsync();
        await handle.DisposeAsync(); // should not throw
    }

    [Fact]
    public async Task ConcurrentAcquire_OnlyOneSucceeds()
    {
        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => _lock.AcquireAsync("shared", TimeSpan.FromSeconds(30))));
        results.Count(r => r != null).Should().Be(1);
        foreach (var h in results.Where(r => r != null))
            await h!.DisposeAsync();
    }
}
