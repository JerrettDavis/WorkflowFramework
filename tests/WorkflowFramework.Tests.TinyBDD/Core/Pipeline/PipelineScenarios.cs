using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Pipeline;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Core.Pipeline;

[Feature("Pipeline — typed transform chain composition")]
public class PipelineScenarios : TinyBddTestBase
{
    public PipelineScenarios(ITestOutputHelper output) : base(output) { }

    // ── single step ───────────────────────────────────────────────────────────

    [Scenario("Single-step pipeline transforms input to output"), Fact]
    public async Task SingleStepTransformsInput()
    {
        var pipe = WorkflowFramework.Pipeline.Pipeline.Create<int>()
            .Pipe<string>((input, ct) => Task.FromResult($"value={input}"))
            .Build();

        var result = await pipe(42, CancellationToken.None);

        await Given("a pipeline that converts int to string", () => result)
            .Then("output is 'value=42'", r => { r.Should().Be("value=42"); return true; })
            .AssertPassed();
    }

    [Scenario("Pipeline.Create<T> with no Pipe steps is an identity transform"), Fact]
    public async Task IdentityPipelineReturnsInput()
    {
        var pipe = WorkflowFramework.Pipeline.Pipeline.Create<string>().Build();
        var result = await pipe("hello", CancellationToken.None);

        await Given("an identity pipeline of type string", () => result)
            .Then("output equals input", r => { r.Should().Be("hello"); return true; })
            .AssertPassed();
    }

    // ── multi-step ────────────────────────────────────────────────────────────

    [Scenario("Multi-step pipeline chains transforms in order"), Fact]
    public async Task MultiStepPipelineChainsTransforms()
    {
        var pipe = WorkflowFramework.Pipeline.Pipeline.Create<int>()
            .Pipe<int>((n, _) => Task.FromResult(n * 2))       // double
            .Pipe<int>((n, _) => Task.FromResult(n + 10))      // add 10
            .Pipe<string>((n, _) => Task.FromResult(n.ToString()))
            .Build();

        var result = await pipe(5, CancellationToken.None);

        await Given("a pipeline: x*2 → +10 → ToString, starting from 5", () => result)
            .Then("result is '20' (5*2=10, 10+10=20)", r => { r.Should().Be("20"); return true; })
            .AssertPassed();
    }

    [Scenario("IPipelineStep type overload executes correctly"), Fact]
    public async Task PipelineStepTypeOverloadExecutes()
    {
        var pipe = WorkflowFramework.Pipeline.Pipeline.Create<string>()
            .Pipe<UpperCaseStep, string>()
            .Build();

        var result = await pipe("hello world", CancellationToken.None);

        await Given("a pipeline using Pipe<UpperCaseStep,string>()", () => result)
            .Then("output is uppercased", r => { r.Should().Be("HELLO WORLD"); return true; })
            .AssertPassed();
    }

    [Scenario("IPipelineStep instance overload executes correctly"), Fact]
    public async Task PipelineStepInstanceOverloadExecutes()
    {
        var step = new UpperCaseStep();
        var pipe = WorkflowFramework.Pipeline.Pipeline.Create<string>()
            .Pipe(step)
            .Build();

        var result = await pipe("world", CancellationToken.None);

        await Given("a pipeline using Pipe(step instance)", () => result)
            .Then("output is uppercased", r => { r.Should().Be("WORLD"); return true; })
            .AssertPassed();
    }

    // ── cancellation ──────────────────────────────────────────────────────────

    [Scenario("Cancelled token causes pipeline to throw OperationCanceledException"), Fact]
    public async Task CancelledTokenCausesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var pipe = WorkflowFramework.Pipeline.Pipeline.Create<int>()
            .Pipe<int>((n, ct) => Task.FromResult(n + 1))  // second step — cancellation checked before first Pipe
            .Build();

        Exception? caught = null;
        try { await pipe(1, cts.Token); }
        catch (OperationCanceledException ex) { caught = ex; }

        await Given("a pipeline invoked with a pre-cancelled token", () => caught)
            .Then("OperationCanceledException is thrown", ex =>
            {
                ex.Should().BeOfType<OperationCanceledException>();
                return true;
            })
            .AssertPassed();
    }

    // ── error propagation ─────────────────────────────────────────────────────

    [Scenario("Exception thrown inside a pipeline step propagates to the caller"), Fact]
    public async Task ExceptionInPipelineStepPropagates()
    {
        var pipe = WorkflowFramework.Pipeline.Pipeline.Create<int>()
            .Pipe<string>((_, _) => throw new InvalidOperationException("pipeline-error"))
            .Build();

        Exception? caught = null;
        try { await pipe(1, CancellationToken.None); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("a pipeline step that throws InvalidOperationException", () => caught)
            .Then("exception propagates to caller", ex =>
            {
                ex.Should().BeOfType<InvalidOperationException>();
                ex!.Message.Should().Be("pipeline-error");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null step passed to Pipe() throws ArgumentNullException"), Fact]
    public async Task NullStepToIPipeThrows()
    {
        Exception? caught = null;
        try
        {
            WorkflowFramework.Pipeline.Pipeline.Create<int>()
                .Pipe<string>((IPipelineStep<int, string>)null!);
        }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null IPipelineStep passed to Pipe()", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null transform delegate passed to Pipe() throws ArgumentNullException"), Fact]
    public async Task NullTransformToIPipeThrows()
    {
        Exception? caught = null;
        try
        {
            WorkflowFramework.Pipeline.Pipeline.Create<int>()
                .Pipe<string>((Func<int, CancellationToken, Task<string>>)null!);
        }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null transform delegate passed to Pipe()", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    // ── type chaining ─────────────────────────────────────────────────────────

    [Scenario("Pipeline can change output type at each step — int → string → char[]"), Fact]
    public async Task PipelineCanChangeOutputTypes()
    {
        var pipe = WorkflowFramework.Pipeline.Pipeline.Create<int>()
            .Pipe<string>((n, _) => Task.FromResult(n.ToString()))
            .Pipe<char[]>((s, _) => Task.FromResult(s.ToCharArray()))
            .Build();

        var result = await pipe(123, CancellationToken.None);

        await Given("a pipeline that converts int→string→char[]", () => result)
            .Then("result is ['1','2','3']", r =>
            {
                r.Should().Equal('1', '2', '3');
                return true;
            })
            .AssertPassed();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed class UpperCaseStep : IPipelineStep<string, string>
    {
        public string Name => "UpperCase";
        public Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default)
            => Task.FromResult(input.ToUpperInvariant());
    }
}
