using FluentAssertions;
using WorkflowFramework.Pipeline;
using Xunit;
using PipelineFactory = WorkflowFramework.Pipeline.Pipeline;

namespace WorkflowFramework.Tests;

public class PipelineTests
{
    [Fact]
    public async Task Pipeline_ChainsTransformations()
    {
        var pipeline = PipelineFactory.Create<int>()
            .Pipe<string>((input, ct) => Task.FromResult(input.ToString()))
            .Pipe<string>((input, ct) => Task.FromResult($"Value: {input}"))
            .Build();

        var result = await pipeline(42, CancellationToken.None);
        result.Should().Be("Value: 42");
    }

    [Fact]
    public async Task Pipeline_WithStepInstances()
    {
        var pipeline = PipelineFactory.Create<string>()
            .Pipe(new ToUpperStep())
            .Pipe(new AddExclamationStep())
            .Build();

        var result = await pipeline("hello", CancellationToken.None);
        result.Should().Be("HELLO!");
    }

    [Fact]
    public async Task Pipeline_Identity()
    {
        var pipeline = PipelineFactory.Create<int>().Build();
        var result = await pipeline(5, CancellationToken.None);
        result.Should().Be(5);
    }

    private class ToUpperStep : IPipelineStep<string, string>
    {
        public string Name => "ToUpper";
        public Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default)
            => Task.FromResult(input.ToUpper());
    }

    private class AddExclamationStep : IPipelineStep<string, string>
    {
        public string Name => "AddExclamation";
        public Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default)
            => Task.FromResult(input + "!");
    }
}
