using System.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Npgsql;
using WorkflowFramework.Extensions.Distributed;
using WorkflowFramework.Extensions.Distributed.PostgreSQL;
using Xunit;

namespace WorkflowFramework.Tests.Extensions;

public class PostgreSqlDistributedLockTests
{
    [Fact]
    public void Constructor_NullFactory_Throws()
    {
        var act = () => new PostgreSqlDistributedLock(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AcquireAsync_NullKey_Throws()
    {
        var lockObj = new PostgreSqlDistributedLock(() => new NpgsqlConnection());
        var act = () => lockObj.AcquireAsync(null!, TimeSpan.FromSeconds(10));
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void GetLockId_SameKey_ReturnsSameValue()
    {
        var id1 = PostgreSqlDistributedLock.GetLockId("test-key");
        var id2 = PostgreSqlDistributedLock.GetLockId("test-key");
        id1.Should().Be(id2);
    }

    [Fact]
    public void GetLockId_DifferentKeys_ReturnsDifferentValues()
    {
        var id1 = PostgreSqlDistributedLock.GetLockId("key-a");
        var id2 = PostgreSqlDistributedLock.GetLockId("key-b");
        id1.Should().NotBe(id2);
    }
}

public class PostgreSqlWorkflowQueueTests
{
    [Fact]
    public void Constructor_NullFactory_Throws()
    {
        var act = () => new PostgreSqlWorkflowQueue(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task EnqueueAsync_NullItem_Throws()
    {
        var queue = new PostgreSqlWorkflowQueue(() => new NpgsqlConnection());
        var act = () => queue.EnqueueAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var queue = new PostgreSqlWorkflowQueue(() => new NpgsqlConnection());
        var act = () => queue.Dispose();
        act.Should().NotThrow();
    }
}

public class PostgreSqlServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPostgreSqlDistributed_NullServices_Throws()
    {
        IServiceCollection services = null!;
        var act = () => services.AddPostgreSqlDistributed("Host=localhost");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPostgreSqlDistributed_NullConnectionString_Throws()
    {
        var services = new ServiceCollection();
        var act = () => services.AddPostgreSqlDistributed(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPostgreSqlDistributed_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlDistributed("Host=localhost;Database=test");

        var provider = services.BuildServiceProvider();
        provider.GetService<IDistributedLock>().Should().BeOfType<PostgreSqlDistributedLock>();
        provider.GetService<IWorkflowQueue>().Should().BeOfType<PostgreSqlWorkflowQueue>();
    }

    [Fact]
    public void AddPostgreSqlDistributed_ReturnsSameCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddPostgreSqlDistributed("Host=localhost");
        result.Should().BeSameAs(services);
    }
}
