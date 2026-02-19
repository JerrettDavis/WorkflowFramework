using FluentAssertions;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class WorkflowResultTests
{
    [Fact]
    public void Constructor_NullContext_ThrowsArgumentNullException()
    {
        var act = () => new WorkflowResult(WorkflowStatus.Completed, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public void Status_ReturnsProvidedStatus()
    {
        var result = new WorkflowResult(WorkflowStatus.Faulted, new WorkflowContext());
        result.Status.Should().Be(WorkflowStatus.Faulted);
    }

    [Fact]
    public void IsSuccess_Completed_ReturnsTrue()
    {
        var result = new WorkflowResult(WorkflowStatus.Completed, new WorkflowContext());
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(WorkflowStatus.Faulted)]
    [InlineData(WorkflowStatus.Aborted)]
    [InlineData(WorkflowStatus.Compensated)]
    [InlineData(WorkflowStatus.Pending)]
    [InlineData(WorkflowStatus.Running)]
    [InlineData(WorkflowStatus.Suspended)]
    public void IsSuccess_NonCompleted_ReturnsFalse(WorkflowStatus status)
    {
        var result = new WorkflowResult(status, new WorkflowContext());
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Context_ReturnsProvidedContext()
    {
        var ctx = new WorkflowContext();
        var result = new WorkflowResult(WorkflowStatus.Completed, ctx);
        result.Context.Should().BeSameAs(ctx);
    }

    [Fact]
    public void Errors_ReturnsContextErrors()
    {
        var ctx = new WorkflowContext();
        ctx.Errors.Add(new WorkflowError("step1", new Exception(), DateTimeOffset.UtcNow));
        var result = new WorkflowResult(WorkflowStatus.Faulted, ctx);
        result.Errors.Should().HaveCount(1);
    }
}

public class WorkflowResultGenericTests
{
    private class TestData { public int Value { get; set; } }

    [Fact]
    public void TypedContext_ReturnsProvidedContext()
    {
        var ctx = new WorkflowContext<TestData>(new TestData { Value = 42 });
        var result = new WorkflowResult<TestData>(WorkflowStatus.Completed, ctx);
        result.TypedContext.Should().BeSameAs(ctx);
    }

    [Fact]
    public void Data_ReturnsContextData()
    {
        var ctx = new WorkflowContext<TestData>(new TestData { Value = 99 });
        var result = new WorkflowResult<TestData>(WorkflowStatus.Completed, ctx);
        result.Data.Value.Should().Be(99);
    }

    [Fact]
    public void InheritsBaseProperties()
    {
        var ctx = new WorkflowContext<TestData>(new TestData());
        var result = new WorkflowResult<TestData>(WorkflowStatus.Completed, ctx);
        result.IsSuccess.Should().BeTrue();
        result.Context.Should().BeSameAs(ctx);
    }
}
