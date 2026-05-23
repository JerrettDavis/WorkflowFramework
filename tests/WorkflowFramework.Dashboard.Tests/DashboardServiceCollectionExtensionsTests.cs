using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
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

    [Fact]
    public void MapWorkflowDashboard_NullEndpoints_ThrowsArgumentNullException()
    {
        IEndpointRouteBuilder? endpoints = null;
        var act = () => DashboardServiceCollectionExtensions.MapWorkflowDashboard(endpoints!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("endpoints");
    }

    [Fact]
    public void MapWorkflowDashboard_ValidEndpoints_ReturnsEndpoints()
    {
        // Use a minimal WebApplication to get a real IEndpointRouteBuilder
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddWorkflowDashboard();
        using var app = builder.Build();

        var result = app.MapWorkflowDashboard();

        result.Should().NotBeNull();
    }

    [Fact]
    public void MapWorkflowDashboard_CustomPathPrefix_UsesPrefix()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddWorkflowDashboard();
        using var app = builder.Build();

        // Should not throw with custom prefix
        var result = app.MapWorkflowDashboard("/custom-path");

        result.Should().NotBeNull();
    }
}
