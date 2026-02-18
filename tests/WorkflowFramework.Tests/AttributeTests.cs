using FluentAssertions;
using WorkflowFramework.Attributes;
using Xunit;

namespace WorkflowFramework.Tests;

public class AttributeTests
{
    [Fact]
    public void StepNameAttribute_StoresName()
    {
        var attr = new StepNameAttribute("MyStep");
        attr.Name.Should().Be("MyStep");
    }

    [Fact]
    public void StepDescriptionAttribute_StoresDescription()
    {
        var attr = new StepDescriptionAttribute("Does something");
        attr.Description.Should().Be("Does something");
    }

    [Fact]
    public void StepTimeoutAttribute_StoresTimeout()
    {
        var attr = new StepTimeoutAttribute(30.5);
        attr.TimeoutSeconds.Should().Be(30.5);
    }

    [Fact]
    public void StepRetryAttribute_StoresValues()
    {
        var attr = new StepRetryAttribute(5, 200);
        attr.MaxAttempts.Should().Be(5);
        attr.BackoffMs.Should().Be(200);
    }

    [Fact]
    public void StepOrderAttribute_StoresOrder()
    {
        var attr = new StepOrderAttribute(3);
        attr.Order.Should().Be(3);
    }

    [Fact]
    public void WorkflowAttribute_StoresName()
    {
        var attr = new WorkflowAttribute("OrderProcessing");
        attr.Name.Should().Be("OrderProcessing");
    }

    [StepName("Decorated")]
    [StepDescription("A decorated step")]
    [StepTimeout(10)]
    [StepRetry(3, 100)]
    [StepOrder(1)]
    private class DecoratedStep : IStep
    {
        public string Name => "Decorated";
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    [Fact]
    public void Attributes_CanBeReadViaReflection()
    {
        var type = typeof(DecoratedStep);
        type.GetCustomAttributes(typeof(StepNameAttribute), false).Should().ContainSingle();
        type.GetCustomAttributes(typeof(StepDescriptionAttribute), false).Should().ContainSingle();
        type.GetCustomAttributes(typeof(StepTimeoutAttribute), false).Should().ContainSingle();
        type.GetCustomAttributes(typeof(StepRetryAttribute), false).Should().ContainSingle();
        type.GetCustomAttributes(typeof(StepOrderAttribute), false).Should().ContainSingle();
    }
}
