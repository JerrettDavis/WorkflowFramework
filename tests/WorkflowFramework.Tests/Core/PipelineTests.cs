using FluentAssertions;
using WorkflowFramework.Pipeline;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class PipelineCoreTests
{
    private class DoubleStep : IPipelineStep<int, int>
    {
        public string Name => "Double";
        public Task<int> ExecuteAsync(int input, CancellationToken ct = default) => Task.FromResult(input * 2);
    }

    private class ToStringStep : IPipelineStep<int, string>
    {
        public string Name => "ToString";
        public Task<string> ExecuteAsync(int input, CancellationToken ct = default) => Task.FromResult(input.ToString());
    }

    [Fact]
    public async Task Pipeline_Identity()
    {
        var fn = WorkflowFramework.Pipeline.Pipeline.Create<int>().Build();
        var result = await fn(42, CancellationToken.None);
        result.Should().Be(42);
    }

    [Fact]
    public async Task Pipeline_SingleStep()
    {
        var fn = WorkflowFramework.Pipeline.Pipeline.Create<int>().Pipe(new DoubleStep()).Build();
        var result = await fn(5, CancellationToken.None);
        result.Should().Be(10);
    }

    [Fact]
    public async Task Pipeline_ChainedSteps()
    {
        var fn = WorkflowFramework.Pipeline.Pipeline.Create<int>()
            .Pipe(new DoubleStep())
            .Pipe(new ToStringStep())
            .Build();
        var result = await fn(5, CancellationToken.None);
        result.Should().Be("10");
    }

    [Fact]
    public async Task Pipeline_DelegateStep()
    {
        var fn = WorkflowFramework.Pipeline.Pipeline.Create<int>()
            .Pipe<int>((val, ct) => Task.FromResult(val + 1))
            .Build();
        var result = await fn(10, CancellationToken.None);
        result.Should().Be(11);
    }

    [Fact]
    public async Task Pipeline_GenericStep()
    {
        var fn = WorkflowFramework.Pipeline.Pipeline.Create<int>()
            .Pipe<DoubleStep, int>()
            .Build();
        var result = await fn(3, CancellationToken.None);
        result.Should().Be(6);
    }

    [Fact]
    public void Pipeline_NullStep_Throws()
    {
        var act = () => WorkflowFramework.Pipeline.Pipeline.Create<int>().Pipe((IPipelineStep<int, int>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Pipeline_NullDelegate_Throws()
    {
        var act = () => WorkflowFramework.Pipeline.Pipeline.Create<int>().Pipe((Func<int, CancellationToken, Task<int>>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Pipeline_CancellationRespected()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var fn = WorkflowFramework.Pipeline.Pipeline.Create<int>()
            .Pipe(new DoubleStep())
            .Build();
        Func<Task> act = async () => await fn(5, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Pipeline_CancellationRespected_Delegate()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var fn = WorkflowFramework.Pipeline.Pipeline.Create<int>()
            .Pipe<int>((val, ct) => Task.FromResult(val))
            .Build();
        Func<Task> act = async () => await fn(5, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
