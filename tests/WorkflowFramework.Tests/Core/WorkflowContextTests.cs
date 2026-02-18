using FluentAssertions;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class WorkflowContextTests
{
    [Fact]
    public void Constructor_Default_GeneratesIds()
    {
        var ctx = new WorkflowContext();
        ctx.WorkflowId.Should().NotBeNullOrEmpty();
        ctx.CorrelationId.Should().NotBeNullOrEmpty();
        ctx.CancellationToken.Should().Be(CancellationToken.None);
    }

    [Fact]
    public void Constructor_WithCancellationToken_StoresToken()
    {
        var cts = new CancellationTokenSource();
        var ctx = new WorkflowContext(cts.Token);
        ctx.CancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public void Properties_IsEmptyByDefault()
    {
        var ctx = new WorkflowContext();
        ctx.Properties.Should().BeEmpty();
    }

    [Fact]
    public void Properties_CanSetAndGet()
    {
        var ctx = new WorkflowContext();
        ctx.Properties["key"] = "value";
        ctx.Properties["key"].Should().Be("value");
    }

    [Fact]
    public void CurrentStepName_DefaultIsNull()
    {
        var ctx = new WorkflowContext();
        ctx.CurrentStepName.Should().BeNull();
    }

    [Fact]
    public void CurrentStepName_CanSetAndGet()
    {
        var ctx = new WorkflowContext();
        ctx.CurrentStepName = "Step1";
        ctx.CurrentStepName.Should().Be("Step1");
    }

    [Fact]
    public void CurrentStepIndex_DefaultIsZero()
    {
        var ctx = new WorkflowContext();
        ctx.CurrentStepIndex.Should().Be(0);
    }

    [Fact]
    public void IsAborted_DefaultIsFalse()
    {
        var ctx = new WorkflowContext();
        ctx.IsAborted.Should().BeFalse();
    }

    [Fact]
    public void IsAborted_CanSetToTrue()
    {
        var ctx = new WorkflowContext();
        ctx.IsAborted = true;
        ctx.IsAborted.Should().BeTrue();
    }

    [Fact]
    public void Errors_IsEmptyByDefault()
    {
        var ctx = new WorkflowContext();
        ctx.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Errors_CanAdd()
    {
        var ctx = new WorkflowContext();
        ctx.Errors.Add(new WorkflowError("step", new Exception("err"), DateTimeOffset.UtcNow));
        ctx.Errors.Should().HaveCount(1);
    }

    [Fact]
    public void WorkflowId_IsDifferentPerInstance()
    {
        var c1 = new WorkflowContext();
        var c2 = new WorkflowContext();
        c1.WorkflowId.Should().NotBe(c2.WorkflowId);
    }
}

public class WorkflowContextGenericTests
{
    private class TestData
    {
        public string Value { get; set; } = "";
    }

    [Fact]
    public void Constructor_NullData_ThrowsArgumentNullException()
    {
        var act = () => new WorkflowContext<TestData>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("data");
    }

    [Fact]
    public void Constructor_ValidData_StoresData()
    {
        var data = new TestData { Value = "hello" };
        var ctx = new WorkflowContext<TestData>(data);
        ctx.Data.Should().BeSameAs(data);
        ctx.Data.Value.Should().Be("hello");
    }

    [Fact]
    public void Constructor_WithCancellationToken()
    {
        var cts = new CancellationTokenSource();
        var ctx = new WorkflowContext<TestData>(new TestData(), cts.Token);
        ctx.CancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public void Data_CanBeReplaced()
    {
        var ctx = new WorkflowContext<TestData>(new TestData { Value = "first" });
        ctx.Data = new TestData { Value = "second" };
        ctx.Data.Value.Should().Be("second");
    }

    [Fact]
    public void InheritsWorkflowContextBehavior()
    {
        var ctx = new WorkflowContext<TestData>(new TestData());
        ctx.WorkflowId.Should().NotBeNullOrEmpty();
        ctx.Properties.Should().BeEmpty();
        ctx.Errors.Should().BeEmpty();
    }
}
