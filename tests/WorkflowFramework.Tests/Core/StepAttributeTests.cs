using FluentAssertions;
using WorkflowFramework.Attributes;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class StepAttributeTests
{
    [Fact]
    public void StepNameAttribute_NullName_Throws()
    {
        var act = () => new StepNameAttribute(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void StepNameAttribute_SetsName()
    {
        var attr = new StepNameAttribute("Test");
        attr.Name.Should().Be("Test");
    }

    [Fact]
    public void StepDescriptionAttribute_NullDescription_Throws()
    {
        var act = () => new StepDescriptionAttribute(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("description");
    }

    [Fact]
    public void StepDescriptionAttribute_SetsDescription()
    {
        var attr = new StepDescriptionAttribute("A description");
        attr.Description.Should().Be("A description");
    }

    [Fact]
    public void StepTimeoutAttribute_SetsTimeout()
    {
        var attr = new StepTimeoutAttribute(30.5);
        attr.TimeoutSeconds.Should().Be(30.5);
    }

    [Fact]
    public void StepRetryAttribute_SetsValues()
    {
        var attr = new StepRetryAttribute(5, 1000);
        attr.MaxAttempts.Should().Be(5);
        attr.BackoffMs.Should().Be(1000);
    }

    [Fact]
    public void StepRetryAttribute_DefaultBackoff_IsZero()
    {
        var attr = new StepRetryAttribute(3);
        attr.BackoffMs.Should().Be(0);
    }

    [Fact]
    public void StepOrderAttribute_SetsOrder()
    {
        var attr = new StepOrderAttribute(10);
        attr.Order.Should().Be(10);
    }

    [Fact]
    public void WorkflowAttribute_NullName_Throws()
    {
        var act = () => new WorkflowAttribute(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void WorkflowAttribute_SetsName()
    {
        var attr = new WorkflowAttribute("OrderWorkflow");
        attr.Name.Should().Be("OrderWorkflow");
    }
}
