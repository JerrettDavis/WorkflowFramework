using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.DependencyInjection;
using Xunit;

namespace WorkflowFramework.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void Given_ServiceCollection_When_AddWorkflowFramework_Then_BuilderResolvable()
    {
        // Given
        var services = new ServiceCollection();

        // When
        services.AddWorkflowFramework();
        var provider = services.BuildServiceProvider();

        // Then
        var builder = provider.GetService<IWorkflowBuilder>();
        builder.Should().NotBeNull();
        builder.Should().BeOfType<WorkflowBuilder>();
    }

    [Fact]
    public void Given_ServiceCollection_When_AddStep_Then_StepResolvable()
    {
        // Given
        var services = new ServiceCollection();
        services.AddStep<TestStep>();

        // When
        var provider = services.BuildServiceProvider();

        // Then
        var step = provider.GetService<TestStep>();
        step.Should().NotBeNull();
    }

    public class TestStep : IStep
    {
        public string Name => "TestStep";
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }
}
