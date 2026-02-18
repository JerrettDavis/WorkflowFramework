using FluentAssertions;
using WorkflowFramework.Extensions.Distributed;
using WorkflowFramework.Extensions.Diagnostics;
using WorkflowFramework.Extensions.Http;
using WorkflowFramework.Testing;
using WorkflowFramework.Builder;
using WorkflowFramework.Registry;
using WorkflowFramework.Versioning;
using WorkflowFramework.Validation;
using Xunit;

namespace WorkflowFramework.Tests;

public class AdditionalTests
{
    // ==========================================
    // InMemoryDistributedLock edge cases
    // ==========================================

    [Fact]
    public async Task InMemoryLock_DifferentKeys_BothAcquire()
    {
        var lockProvider = new InMemoryDistributedLock();
        var h1 = await lockProvider.AcquireAsync("a", TimeSpan.FromSeconds(30));
        var h2 = await lockProvider.AcquireAsync("b", TimeSpan.FromSeconds(30));

        h1.Should().NotBeNull();
        h2.Should().NotBeNull();
        await h1!.DisposeAsync();
        await h2!.DisposeAsync();
    }

    [Fact]
    public async Task InMemoryLock_DoubleDispose_Safe()
    {
        var lockProvider = new InMemoryDistributedLock();
        var h = await lockProvider.AcquireAsync("x", TimeSpan.FromSeconds(30));
        await h!.DisposeAsync();
        await h.DisposeAsync(); // should not throw
    }

    // ==========================================
    // InMemoryWorkflowQueue edge cases
    // ==========================================

    [Fact]
    public async Task InMemoryQueue_MultipleEnqueueDequeue()
    {
        var queue = new InMemoryWorkflowQueue();
        for (var i = 0; i < 10; i++)
            await queue.EnqueueAsync(new WorkflowQueueItem { WorkflowName = $"W{i}" });

        var length = await queue.GetLengthAsync();
        length.Should().Be(10);

        for (var i = 0; i < 10; i++)
        {
            var item = await queue.DequeueAsync();
            item!.WorkflowName.Should().Be($"W{i}");
        }

        (await queue.GetLengthAsync()).Should().Be(0);
    }

    [Fact]
    public async Task InMemoryQueue_EnqueueNull_Throws()
    {
        var queue = new InMemoryWorkflowQueue();
        await Assert.ThrowsAsync<ArgumentNullException>(() => queue.EnqueueAsync(null!));
    }

    // ==========================================
    // MetricsMiddleware edge cases
    // ==========================================

    [Fact]
    public void MetricsMiddleware_Initial_AllZero()
    {
        var m = new MetricsMiddleware();
        m.TotalSteps.Should().Be(0);
        m.FailedSteps.Should().Be(0);
        m.AverageDuration.Should().Be(TimeSpan.Zero);
    }

    // ==========================================
    // MockStep edge cases
    // ==========================================

    [Fact]
    public async Task MockStep_WithAction_ExecutesAction()
    {
        var called = false;
        var mock = new MockStep("Test", _ => { called = true; return Task.CompletedTask; });
        await mock.ExecuteAsync(new WorkflowContext());
        called.Should().BeTrue();
        mock.InvocationCount.Should().Be(1);
    }

    [Fact]
    public void MockStep_NullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MockStep(null!));
    }

    // ==========================================
    // WorkflowAssertions negative tests
    // ==========================================

    [Fact]
    public async Task WorkflowAssertions_ShouldBeCompleted_ThrowsWhenFaulted()
    {
        var workflow = Workflow.Create("Test").Step("Fail", _ => throw new Exception()).Build();
        var result = await workflow.ExecuteAsync(new WorkflowContext());

        Assert.Throws<InvalidOperationException>(() => result.ShouldBeCompleted());
    }

    [Fact]
    public async Task WorkflowAssertions_ShouldBeFaulted_ThrowsWhenCompleted()
    {
        var workflow = Workflow.Create("Test").Step("Ok", _ => Task.CompletedTask).Build();
        var result = await workflow.ExecuteAsync(new WorkflowContext());

        Assert.Throws<InvalidOperationException>(() => result.ShouldBeFaulted());
    }

    [Fact]
    public async Task WorkflowAssertions_ShouldHaveProperty_ThrowsWhenMissing()
    {
        var workflow = Workflow.Create("Test").Step("Ok", _ => Task.CompletedTask).Build();
        var result = await workflow.ExecuteAsync(new WorkflowContext());

        Assert.Throws<InvalidOperationException>(() => result.ShouldHaveProperty("missing"));
    }

    [Fact]
    public async Task WorkflowAssertions_ShouldHaveProperty_ThrowsOnWrongValue()
    {
        var workflow = Workflow.Create("Test")
            .Step("Set", ctx => { ctx.Properties["k"] = "actual"; return Task.CompletedTask; })
            .Build();
        var result = await workflow.ExecuteAsync(new WorkflowContext());

        Assert.Throws<InvalidOperationException>(() => result.ShouldHaveProperty("k", "expected"));
    }

    [Fact]
    public async Task WorkflowAssertions_ShouldHaveNoErrors_ThrowsWhenErrors()
    {
        var workflow = Workflow.Create("Test").Step("Fail", _ => throw new Exception()).Build();
        var result = await workflow.ExecuteAsync(new WorkflowContext());

        Assert.Throws<InvalidOperationException>(() => result.ShouldHaveNoErrors());
    }

    // ==========================================
    // StepBase<TData> tests
    // ==========================================

    [Fact]
    public void StepBase_Typed_Name_UsesTypeName()
    {
        var step = new TypedTestStep();
        step.Name.Should().Be("TypedTestStep");
    }

    [Fact]
    public async Task StepBase_Typed_Executes()
    {
        var step = new TypedTestStep();
        var ctx = new WorkflowContext<TestData>(new TestData());
        await step.ExecuteAsync(ctx);
        ctx.Data.Value.Should().Be("executed");
    }

    // ==========================================
    // WorkflowException hierarchy
    // ==========================================

    [Fact]
    public void WorkflowException_WithInner()
    {
        var inner = new Exception("inner");
        var ex = new WorkflowException("outer", inner);
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void WorkflowAbortedException_IsWorkflowException()
    {
        var ex = new WorkflowAbortedException("wf");
        (ex is WorkflowException).Should().BeTrue();
    }

    [Fact]
    public void StepExecutionException_IsWorkflowException()
    {
        var ex = new StepExecutionException("s", new Exception());
        (ex is WorkflowException).Should().BeTrue();
    }

    // ==========================================
    // HttpStepOptions tests
    // ==========================================

    [Fact]
    public void HttpStepOptions_CanSetAll()
    {
        var opts = new HttpStepOptions
        {
            Name = "Test",
            Url = "http://test.com",
            Method = HttpMethod.Put,
            Body = "body",
            ContentType = "text/plain",
            EnsureSuccessStatusCode = false
        };
        opts.Headers["X-Custom"] = "val";

        opts.Name.Should().Be("Test");
        opts.Url.Should().Be("http://test.com");
        opts.Method.Should().Be(HttpMethod.Put);
        opts.Headers.Should().ContainKey("X-Custom");
    }

    [Fact]
    public void HttpBuilderExtensions_HttpPut()
    {
        var workflow = Workflow.Create("T").HttpPut("http://example.com", "{}").Build();
        workflow.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void HttpBuilderExtensions_HttpDelete()
    {
        var workflow = Workflow.Create("T").HttpDelete("http://example.com").Build();
        workflow.Steps.Should().HaveCount(1);
    }

    // Helper classes
    private class TestData
    {
        public string Value { get; set; } = "";
    }

    private class TypedTestStep : StepBase<TestData>
    {
        public override Task ExecuteAsync(IWorkflowContext<TestData> context)
        {
            context.Data.Value = "executed";
            return Task.CompletedTask;
        }
    }
}
