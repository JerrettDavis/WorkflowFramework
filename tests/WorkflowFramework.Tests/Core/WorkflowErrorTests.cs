using FluentAssertions;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class WorkflowErrorTests
{
    [Fact]
    public void Constructor_NullStepName_Throws()
    {
        var act = () => new WorkflowError(null!, new Exception(), DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentNullException>().WithParameterName("stepName");
    }

    [Fact]
    public void Constructor_NullException_Throws()
    {
        var act = () => new WorkflowError("step", null!, DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentNullException>().WithParameterName("exception");
    }

    [Fact]
    public void Properties_SetCorrectly()
    {
        var ex = new InvalidOperationException("test");
        var ts = DateTimeOffset.UtcNow;
        var error = new WorkflowError("MyStep", ex, ts);
        error.StepName.Should().Be("MyStep");
        error.Exception.Should().BeSameAs(ex);
        error.Timestamp.Should().Be(ts);
    }
}
