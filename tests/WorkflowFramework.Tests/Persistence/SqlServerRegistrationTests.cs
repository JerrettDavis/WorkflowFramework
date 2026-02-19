using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Persistence.EntityFramework;
using WorkflowFramework.Extensions.Persistence.SqlServer;
using WorkflowFramework.Persistence;
using Xunit;

namespace WorkflowFramework.Tests.Persistence;

public class SqlServerRegistrationTests
{
    [Fact]
    public void AddSqlServerPersistence_RegistersRequiredServices()
    {
        var services = new ServiceCollection();

        services.AddSqlServerPersistence("Server=localhost;Database=test;TrustServerCertificate=true");

        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IWorkflowStateStore>();
        store.Should().NotBeNull();
    }

    [Fact]
    public void AddSqlServerPersistence_RegistersDbContext()
    {
        var services = new ServiceCollection();

        services.AddSqlServerPersistence("Server=localhost;Database=test;TrustServerCertificate=true");

        var provider = services.BuildServiceProvider();
        var context = provider.GetService<WorkflowDbContext>();
        context.Should().NotBeNull();
    }

    [Fact]
    public void AddSqlServerPersistence_ConfiguresSqlServerProvider()
    {
        var services = new ServiceCollection();

        services.AddSqlServerPersistence("Server=localhost;Database=test;TrustServerCertificate=true");

        var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<WorkflowDbContext>();
        context.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.SqlServer");
    }

    [Fact]
    public void SqlServerWorkflowDbContext_UsesSqlServerProvider()
    {
        var context = new SqlServerWorkflowDbContext("Server=localhost;Database=test;TrustServerCertificate=true");

        context.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.SqlServer");
    }
}
