using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Persistence.EntityFramework;
using WorkflowFramework.Extensions.Persistence.PostgreSQL;
using WorkflowFramework.Persistence;
using Xunit;

namespace WorkflowFramework.Tests.Persistence;

public class PostgreSqlRegistrationTests
{
    [Fact]
    public void AddPostgreSqlPersistence_RegistersRequiredServices()
    {
        var services = new ServiceCollection();

        services.AddPostgreSqlPersistence("Host=localhost;Database=test");

        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IWorkflowStateStore>();
        store.Should().NotBeNull();
    }

    [Fact]
    public void AddPostgreSqlPersistence_RegistersDbContext()
    {
        var services = new ServiceCollection();

        services.AddPostgreSqlPersistence("Host=localhost;Database=test");

        var provider = services.BuildServiceProvider();
        var context = provider.GetService<WorkflowDbContext>();
        context.Should().NotBeNull();
    }

    [Fact]
    public void AddPostgreSqlPersistence_ConfiguresNpgsqlProvider()
    {
        var services = new ServiceCollection();

        services.AddPostgreSqlPersistence("Host=localhost;Database=test");

        var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<WorkflowDbContext>();
        context.Database.ProviderName.Should().Be("Npgsql.EntityFrameworkCore.PostgreSQL");
    }

    [Fact]
    public void PostgreSqlWorkflowDbContext_UsesNpgsqlProvider()
    {
        var context = new PostgreSqlWorkflowDbContext("Host=localhost;Database=test");

        context.Database.ProviderName.Should().Be("Npgsql.EntityFrameworkCore.PostgreSQL");
    }
}
