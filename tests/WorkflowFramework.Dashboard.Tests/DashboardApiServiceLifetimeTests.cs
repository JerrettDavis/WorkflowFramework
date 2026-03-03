#if NET10_0
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Dashboard.Api;
using WorkflowFramework.Dashboard.Api.Services;
using Xunit;

namespace WorkflowFramework.Dashboard.Tests;

public sealed class DashboardApiServiceLifetimeTests
{
    [Fact]
    public void AddWorkflowDashboardApi_RegistersExpectedServiceLifetimes()
    {
        var services = new ServiceCollection();

        services.AddWorkflowDashboardApi();

        services.Should().ContainSingle(s => s.ServiceType == typeof(WorkflowDefinitionCompiler))
            .Which.Lifetime.Should().Be(ServiceLifetime.Scoped);
        services.Should().ContainSingle(s => s.ServiceType == typeof(WorkflowRunService))
            .Which.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
}
#endif
