using FluentAssertions;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class WorkflowExceptionTests
{
    [Fact]
    public void WorkflowException_MessageOnly()
    {
        var ex = new WorkflowException("test error");
        ex.Message.Should().Be("test error");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void WorkflowException_WithInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new WorkflowException("outer", inner);
        ex.Message.Should().Be("outer");
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void WorkflowAbortedException_SetsWorkflowId()
    {
        var ex = new WorkflowAbortedException("wf123");
        ex.WorkflowId.Should().Be("wf123");
        ex.Message.Should().Contain("wf123");
    }

    [Fact]
    public void WorkflowAbortedException_InheritsFromWorkflowException()
    {
        var ex = new WorkflowAbortedException("wf");
        ex.Should().BeAssignableTo<WorkflowException>();
    }

    [Fact]
    public void StepExecutionException_SetsStepName()
    {
        var inner = new Exception("fail");
        var ex = new StepExecutionException("MyStep", inner);
        ex.StepName.Should().Be("MyStep");
        ex.InnerException.Should().BeSameAs(inner);
        ex.Message.Should().Contain("MyStep");
    }

    [Fact]
    public void StepExecutionException_InheritsFromWorkflowException()
    {
        var ex = new StepExecutionException("s", new Exception());
        ex.Should().BeAssignableTo<WorkflowException>();
    }
}
