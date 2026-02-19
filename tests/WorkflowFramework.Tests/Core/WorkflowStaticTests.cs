using FluentAssertions;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class WorkflowStaticTests
{
    [Fact]
    public void Create_ReturnsBuilder()
    {
        Workflow.Create().Should().NotBeNull();
    }

    [Fact]
    public void Create_WithName_SetsName()
    {
        var wf = Workflow.Create("MyWf").Build();
        wf.Name.Should().Be("MyWf");
    }

    [Fact]
    public void CreateGeneric_ReturnsTypedBuilder()
    {
        Workflow.Create<TestData>().Should().NotBeNull();
    }

    [Fact]
    public void CreateGeneric_WithName_SetsName()
    {
        var wf = Workflow.Create<TestData>("TypedWf").Build();
        wf.Name.Should().Be("TypedWf");
    }

    private class TestData { }
}
