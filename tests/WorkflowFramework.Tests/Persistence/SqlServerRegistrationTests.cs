using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Persistence.SqlServer;
using WorkflowFramework.Persistence;
using Xunit;

namespace WorkflowFramework.Tests.Persistence;

public class SqlServerRegistrationTests
{
    [Fact]
    public void AddWorkflowSqlServer_RegistersRequiredServices()
    {
        var services = new ServiceCollection();

        services.AddWorkflowSqlServer("Server=localhost;Database=test;TrustServerCertificate=true");

        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IWorkflowStateStore>();
        store.Should().NotBeNull();
    }
}
