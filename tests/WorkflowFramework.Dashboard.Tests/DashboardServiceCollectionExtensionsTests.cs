using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Dashboard;
using WorkflowFramework.Dashboard.Services;
using Xunit;

namespace WorkflowFramework.Dashboard.Tests;

/// <summary>
/// Characterization tests for DashboardServiceCollectionExtensions (Phase I coverage).
/// </summary>
public sealed class DashboardServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWorkflowDashboard_RegistersWorkflowDashboardService()
    {
        var services = new ServiceCollection();
        // WorkflowDashboardService requires IWorkflowRegistry and IWorkflowEngine — provide stubs.
        services.AddWorkflowDashboard();

        services.Should().ContainSingle(s => s.ServiceType == typeof(WorkflowDashboardService));
    }

    [Fact]
    public void AddWorkflowDashboard_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;
        var act = () => DashboardServiceCollectionExtensions.AddWorkflowDashboard(services!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }
}
