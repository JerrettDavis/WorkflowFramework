using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Persistence.PostgreSQL;
using WorkflowFramework.Persistence;
using Xunit;

namespace WorkflowFramework.Tests.Persistence;

public class PostgreSqlRegistrationTests
{
    [Fact]
    public void AddWorkflowPostgreSql_RegistersRequiredServices()
    {
        var services = new ServiceCollection();

        services.AddWorkflowPostgreSql("Host=localhost;Database=test");

        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IWorkflowStateStore>();
        store.Should().NotBeNull();
    }
}
