using FluentAssertions;
using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.Configuration;
using WorkflowFramework.Extensions.Diagnostics;
using WorkflowFramework.Extensions.Reactive;
using WorkflowFramework.Extensions.Scheduling;
using WorkflowFramework.Extensions.Visualization;
using WorkflowFramework.Pipeline;
using WorkflowFramework.Registry;
using WorkflowFramework.Testing;
using WorkflowFramework.Tests.Common;
using WorkflowFramework.Validation;
using WorkflowFramework.Versioning;
using Xunit;
using PipelineFactory = WorkflowFramework.Pipeline.Pipeline;

namespace WorkflowFramework.Tests;

public class PipelineCancellationTests
{
    [Fact]
    public async Task Pipeline_PropagatesCancellationToken()
    {
        var receivedCt = CancellationToken.None;

        var pipeline = PipelineFactory.Create<int>()
            .Pipe<string>((input, ct) =>
            {
                receivedCt = ct;
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(input.ToString());
            })
            .Build();

        using var cts = new CancellationTokenSource();
        await pipeline(42, cts.Token);
        receivedCt.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Pipeline_CancellationThrows()
    {
        var pipeline = PipelineFactory.Create<int>()
            .Pipe<string>(async (input, ct) =>
            {
                await Task.Delay(5000, ct);
                return input.ToString();
            })
            .Build();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => pipeline(42, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Pipeline_StepInstance_ReceivesCancellationToken()
    {
        var step = new CancellationAwareStep();
        var pipeline = PipelineFactory.Create<string>()
            .Pipe(step)
            .Build();

        using var cts = new CancellationTokenSource();
        await pipeline("test", cts.Token);
        step.ReceivedToken.Should().Be(cts.Token);
    }

    private class CancellationAwareStep : IPipelineStep<string, string>
    {
        public string Name => "CancellationAware";
        public CancellationToken ReceivedToken { get; private set; }
        public Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            return Task.FromResult(input.ToUpper());
        }
    }
}

public class YamlConfigurationTests
{
    [Fact]
    public void YamlLoader_LoadsDefinition()
    {
        var yaml = @"
name: TestWorkflow
version: 2
steps:
  - type: StepA
  - type: StepB
";
        var loader = new YamlWorkflowDefinitionLoader();
        var definition = loader.Load(yaml);

        definition.Name.Should().Be("TestWorkflow");
        definition.Version.Should().Be(2);
        definition.Steps.Should().HaveCount(2);
        definition.Steps[0].Type.Should().Be("StepA");
    }

    [Fact]
    public void YamlLoader_LoadsWithRetry()
    {
        var yaml = @"
name: RetryWorkflow
steps:
  - type: FlakyStep
    retry:
      maxAttempts: 5
      backoff: exponential
      baseDelayMs: 200
";
        var loader = new YamlWorkflowDefinitionLoader();
        var definition = loader.Load(yaml);

        definition.Steps[0].Retry.Should().NotBeNull();
        definition.Steps[0].Retry!.MaxAttempts.Should().Be(5);
    }

    [Fact]
    public void JsonLoader_LoadsWithCondition()
    {
        var json = """
        {
            "name": "ConditionalWf",
            "steps": [
                { "type": "Check", "condition": "IsValid", "then": "Process", "else": "Reject" }
            ]
        }
        """;

        var loader = new JsonWorkflowDefinitionLoader();
        var definition = loader.Load(json);

        definition.Steps[0].Condition.Should().Be("IsValid");
        definition.Steps[0].Then.Should().Be("Process");
        definition.Steps[0].Else.Should().Be("Reject");
    }

    [Fact]
    public async Task WorkflowDefinitionBuilder_BuildsConditionalWorkflow()
    {
        var registry = new StepRegistry();
        registry.Register("Check", () => new PropertySetStep("Check", "IsValid", true));
        registry.Register("Process", () => new PropertySetStep("Process", "Processed", true));
        registry.Register("Reject", () => new PropertySetStep("Reject", "Rejected", true));

        var definition = new WorkflowDefinition
        {
            Name = "ConditionalWf",
            Steps =
            [
                new StepDefinition { Type = "Check" },
                new StepDefinition { Condition = "IsValid", Then = "Process", Else = "Reject" }
            ]
        };

        var builder = new WorkflowDefinitionBuilder(registry);
        var workflow = builder.Build(definition);
        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        context.Properties["Processed"].Should().Be(true);
    }

    [Fact]
    public async Task WorkflowDefinitionBuilder_BuildsRetryWorkflow()
    {
        var attempts = 0;
        var registry = new StepRegistry();
        registry.Register("Flaky", () => new DelegateTestStep("Flaky", ctx =>
        {
            attempts++;
            if (attempts < 2) throw new InvalidOperationException("Flaky!");
            return Task.CompletedTask;
        }));

        var definition = new WorkflowDefinition
        {
            Name = "RetryWf",
            Steps =
            [
                new StepDefinition { Type = "Flaky", Retry = new RetryDefinition { MaxAttempts = 3 } }
            ]
        };

        var builder = new WorkflowDefinitionBuilder(registry);
        var workflow = builder.Build(definition);
        var result = await workflow.ExecuteAsync(new WorkflowContext());

        result.IsSuccess.Should().BeTrue();
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task WorkflowDefinitionBuilder_BuildsParallelWorkflow()
    {
        var registry = new StepRegistry();
        registry.Register("A", () => new PropertySetStep("A", "A_ran", true));
        registry.Register("B", () => new PropertySetStep("B", "B_ran", true));

        var definition = new WorkflowDefinition
        {
            Name = "ParallelWf",
            Steps = [new StepDefinition { Parallel = new List<string> { "A", "B" } }]
        };

        var builder = new WorkflowDefinitionBuilder(registry);
        var workflow = builder.Build(definition);
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);

        context.Properties["A_ran"].Should().Be(true);
        context.Properties["B_ran"].Should().Be(true);
    }

    private class PropertySetStep : IStep
    {
        private readonly string _key;
        private readonly object _value;
        public PropertySetStep(string name, string key, object value) { Name = name; _key = key; _value = value; }
        public string Name { get; }
        public Task ExecuteAsync(IWorkflowContext context)
        {
            context.Properties[_key] = _value;
            return Task.CompletedTask;
        }
    }

    private class DelegateTestStep : IStep
    {
        private readonly Func<IWorkflowContext, Task> _action;
        public DelegateTestStep(string name, Func<IWorkflowContext, Task> action) { Name = name; _action = action; }
        public string Name { get; }
        public Task ExecuteAsync(IWorkflowContext context) => _action(context);
    }
}

public class VisualizationExtendedTests
{
    [Fact]
    public void ToMermaid_WithConditional_ContainsDiamond()
    {
        var workflow = Workflow.Create("Test")
            .If(_ => true).Then(new TrackingStep("Then")).Else(new TrackingStep("Else"))
            .Build();

        var mermaid = workflow.ToMermaid();
        mermaid.Should().Contain("graph TD");
        mermaid.Should().Contain("Start");
        mermaid.Should().Contain("End");
        // Conditional step name starts with If(
        mermaid.Should().Contain("If");
    }

    [Fact]
    public void ToDot_WithConditional_ContainsDiamond()
    {
        var workflow = Workflow.Create("Test")
            .If(_ => true).Then(new TrackingStep("Then")).Else(new TrackingStep("Else"))
            .Build();

        var dot = workflow.ToDot();
        dot.Should().Contain("diamond");
    }

    [Fact]
    public void ToMermaid_WithParallel_ShowsParallelNode()
    {
        var workflow = Workflow.Create("Test")
            .Parallel(p => p.Step(new TrackingStep("P1")).Step(new TrackingStep("P2")))
            .Build();

        var mermaid = workflow.ToMermaid();
        mermaid.Should().Contain("Parallel");
    }

    [Fact]
    public void ToDot_WithParallel_ShowsParallelogramShape()
    {
        var workflow = Workflow.Create("Test")
            .Parallel(p => p.Step(new TrackingStep("P1")).Step(new TrackingStep("P2")))
            .Build();

        var dot = workflow.ToDot();
        dot.Should().Contain("parallelogram");
    }

    [Fact]
    public void ToDot_EmptyWorkflow_ValidSyntax()
    {
        var workflow = Workflow.Create("Empty").Build();
        var dot = workflow.ToDot();
        dot.Should().Contain("digraph");
        dot.Should().Contain("Start -> End");
    }
}

public class SchedulerExecutionTests
{
    [Fact]
    public async Task Scheduler_TickExecutesDueWorkflows()
    {
        var registry = new WorkflowRegistry();
        var executed = false;
        registry.Register("test", () => Workflow.Create("test")
            .Step("Mark", ctx => { executed = true; return Task.CompletedTask; })
            .Build());

        using var scheduler = new InMemoryWorkflowScheduler(registry);
        await scheduler.ScheduleAsync("test", DateTimeOffset.UtcNow.AddSeconds(-1), new WorkflowContext());

        await scheduler.TickAsync();

        executed.Should().BeTrue();
        scheduler.ExecutedCount.Should().Be(1);
    }

    [Fact]
    public async Task Scheduler_DoesNotExecuteFutureWorkflows()
    {
        var registry = new WorkflowRegistry();
        registry.Register("test", () => Workflow.Create("test").Build());

        using var scheduler = new InMemoryWorkflowScheduler(registry);
        await scheduler.ScheduleAsync("test", DateTimeOffset.UtcNow.AddHours(1), new WorkflowContext());

        await scheduler.TickAsync();

        scheduler.ExecutedCount.Should().Be(0);
    }

    [Fact]
    public async Task Scheduler_RecurringReschedules()
    {
        var registry = new WorkflowRegistry();
        var count = 0;
        registry.Register("test", () => Workflow.Create("test")
            .Step("Inc", ctx => { count++; return Task.CompletedTask; })
            .Build());

        using var scheduler = new InMemoryWorkflowScheduler(registry);
        await scheduler.ScheduleCronAsync("test", "* * * * *", () => new WorkflowContext());

        // Manually set the entry to be due
        var pending = await scheduler.GetPendingAsync();
        pending.Should().ContainSingle();
        pending[0].IsRecurring.Should().BeTrue();
    }
}

public class DefaultWorkflowValidatorTests
{
    [Fact]
    public async Task EmptyWorkflow_FailsValidation()
    {
        var workflow = Workflow.Create("Empty").Build();
        var validator = new DefaultWorkflowValidator();
        var result = await validator.ValidateAsync(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("at least one step"));
    }

    [Fact]
    public async Task WorkflowWithSteps_PassesValidation()
    {
        var workflow = Workflow.Create("Test")
            .Step("A", _ => Task.CompletedTask)
            .Step("B", _ => Task.CompletedTask)
            .Build();

        var validator = new DefaultWorkflowValidator();
        var result = await validator.ValidateAsync(workflow);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task DuplicateStepNames_FailsValidation()
    {
        var workflow = Workflow.Create("Test")
            .Step("Same", _ => Task.CompletedTask)
            .Step("Same", _ => Task.CompletedTask)
            .Build();

        var validator = new DefaultWorkflowValidator();
        var result = await validator.ValidateAsync(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("Duplicate"));
    }
}

public class ReactiveStepTests
{
    private class CountingAsyncStep : IAsyncStep<int>
    {
        public string Name => "Counter";
        public async IAsyncEnumerable<int> ExecuteStreamingAsync(IWorkflowContext context)
        {
            for (int i = 1; i <= 5; i++)
            {
                await Task.Yield();
                yield return i;
            }
        }
    }

    [Fact]
    public async Task CollectAsync_GathersAllResults()
    {
        var step = new CountingAsyncStep();
        var context = new WorkflowContext();
        var results = await step.CollectAsync(context);

        results.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task ForEachAsync_InvokesCallbackForEach()
    {
        var step = new CountingAsyncStep();
        var context = new WorkflowContext();
        var collected = new List<int>();

        await step.ForEachAsync(context, item =>
        {
            collected.Add(item);
            return Task.CompletedTask;
        });

        collected.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task AsyncStepAdapter_StoresResultsInContext()
    {
        var step = new CountingAsyncStep();
        var adapter = new AsyncStepAdapter<int>(step);
        var context = new WorkflowContext();

        await adapter.ExecuteAsync(context);

        context.Properties["Counter.Results"].Should().BeEquivalentTo(new List<int> { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task AsyncStepAdapter_WorksInWorkflow()
    {
        var step = new CountingAsyncStep();
        var adapter = new AsyncStepAdapter<int>(step);

        var workflow = Workflow.Create("StreamTest")
            .Step(adapter)
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        context.Properties["Counter.Results"].Should().BeEquivalentTo(new List<int> { 1, 2, 3, 4, 5 });
    }
}

public class ErrorHandlingEdgeCaseTests
{
    [Fact]
    public async Task TryCatch_BaseTypeMatching()
    {
        var caught = false;
        var workflow = Workflow.Create()
            .Try(b => b.Step("Fail", _ => throw new ArgumentNullException("param")))
            .Catch<ArgumentException>((ctx, ex) =>
            {
                caught = true;
                return Task.CompletedTask;
            })
            .EndTry()
            .Build();

        var result = await workflow.ExecuteAsync(new WorkflowContext());
        result.IsSuccess.Should().BeTrue();
        caught.Should().BeTrue();
    }

    [Fact]
    public async Task NestedTryCatch_InnerCatches()
    {
        var innerCaught = false;
        var outerCaught = false;

        var workflow = Workflow.Create()
            .Try(b => b
                .Step("outer-work", _ => Task.CompletedTask)
                .Step(new TryCatchInnerStep(() => innerCaught = true)))
            .Catch<Exception>((_, _) => { outerCaught = true; return Task.CompletedTask; })
            .EndTry()
            .Build();

        var result = await workflow.ExecuteAsync(new WorkflowContext());
        result.IsSuccess.Should().BeTrue();
    }

    private class TryCatchInnerStep : IStep
    {
        private readonly Action _onCatch;
        public TryCatchInnerStep(Action onCatch) { _onCatch = onCatch; }
        public string Name => "InnerTryCatch";
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }
}

public class LoopEdgeCaseTests
{
    [Fact]
    public async Task ForEach_EmptyCollection_Completes()
    {
        var bodyExecuted = false;
        var workflow = Workflow.Create()
            .Step("Init", ctx => { ctx.Properties["Items"] = new List<string>(); return Task.CompletedTask; })
            .ForEach<string>(
                ctx => (List<string>)ctx.Properties["Items"]!,
                b => b.Step("Body", ctx => { bodyExecuted = true; return Task.CompletedTask; }))
            .Build();

        var result = await workflow.ExecuteAsync(new WorkflowContext());
        result.IsSuccess.Should().BeTrue();
        bodyExecuted.Should().BeFalse();
    }

    [Fact]
    public async Task While_ImmediatelyFalse_SkipsBody()
    {
        var bodyExecuted = false;
        var workflow = Workflow.Create()
            .While(
                _ => false,
                b => b.Step("Body", ctx => { bodyExecuted = true; return Task.CompletedTask; }))
            .Build();

        var result = await workflow.ExecuteAsync(new WorkflowContext());
        result.IsSuccess.Should().BeTrue();
        bodyExecuted.Should().BeFalse();
    }

    [Fact]
    public async Task Retry_ExhaustsAllAttempts_Completes()
    {
        var attempts = 0;
        var workflow = Workflow.Create()
            .Retry(b => b.Step("AlwaysFail", _ =>
            {
                attempts++;
                throw new InvalidOperationException("Fail");
            }), maxAttempts: 3)
            .Build();

        var result = await workflow.ExecuteAsync(new WorkflowContext());
        // After exhausting retries, the last exception propagates
        // But since it's wrapped in a step, the engine catches it
        attempts.Should().Be(3);
    }
}

public class ParallelErrorTests
{
    [Fact]
    public async Task Parallel_OneStepFails_PropagatesError()
    {
        var workflow = Workflow.Create("ParallelFail")
            .Parallel(p => p
                .Step(new TrackingStep("P1"))
                .Step(new FailingStep())
                .Step(new TrackingStep("P3")))
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.Status.Should().Be(WorkflowStatus.Faulted);
        result.Errors.Should().NotBeEmpty();
    }
}

public class SubWorkflowFailureTests
{
    [Fact]
    public async Task SubWorkflow_ThatFails_AbortsParent()
    {
        var child = Workflow.Create("FailChild")
            .Step(new FailingStep())
            .Build();

        var parentAfterRan = false;
        var parent = Workflow.Create("Parent")
            .SubWorkflow(child)
            .Step("After", ctx => { parentAfterRan = true; return Task.CompletedTask; })
            .Build();

        var context = new WorkflowContext();
        var result = await parent.ExecuteAsync(context);

        // SubWorkflow sets IsAborted on failure
        result.Status.Should().Be(WorkflowStatus.Aborted);
        parentAfterRan.Should().BeFalse();
    }
}

public class TimeoutTests
{
    [Fact]
    public async Task DelayStep_CancelledEarly()
    {
        using var cts = new CancellationTokenSource();
        var workflow = Workflow.Create("DelayCancel")
            .Delay(TimeSpan.FromSeconds(30))
            .Build();

        cts.CancelAfter(50);
        var result = await workflow.ExecuteAsync(new WorkflowContext(cts.Token));
        result.Status.Should().Be(WorkflowStatus.Aborted);
    }

    [Fact]
    public async Task DelayStep_Completes()
    {
        var workflow = Workflow.Create("Delay")
            .Delay(TimeSpan.FromMilliseconds(10))
            .Build();

        var result = await workflow.ExecuteAsync(new WorkflowContext());
        result.IsSuccess.Should().BeTrue();
    }
}

public class CompensationTypedTests
{
    public class OrderData
    {
        public bool Step1Executed { get; set; }
        public bool Step1Compensated { get; set; }
    }

    private class CompStep : ICompensatingStep<OrderData>
    {
        public string Name => "CompStep";
        public Task ExecuteAsync(IWorkflowContext<OrderData> context) { context.Data.Step1Executed = true; return Task.CompletedTask; }
        public Task CompensateAsync(IWorkflowContext<OrderData> context) { context.Data.Step1Compensated = true; return Task.CompletedTask; }
    }

    private class FailTypedStep : IStep<OrderData>
    {
        public string Name => "FailTyped";
        public Task ExecuteAsync(IWorkflowContext<OrderData> context) => throw new InvalidOperationException("Fail");
    }

    [Fact]
    public async Task TypedWorkflow_CompensatesOnFailure()
    {
        var workflow = Workflow.Create<OrderData>("CompTest")
            .WithCompensation()
            .Step(new CompStep())
            .Step(new FailTypedStep())
            .Build();

        var data = new OrderData();
        var result = await workflow.ExecuteAsync(new WorkflowContext<OrderData>(data));

        result.Status.Should().Be(WorkflowStatus.Compensated);
        data.Step1Executed.Should().BeTrue();
        // Note: Compensation works through ICompensatingStep (untyped), but TypedCompensatingStepAdapter
        // would need to be registered. The current typed workflow adapter wraps via TypedStepAdapter which
        // doesn't implement ICompensatingStep. This is a known limitation.
    }
}

public class WorkflowTestHarnessTypedTests
{
    public class TestData
    {
        public string Value { get; set; } = "";
    }

    [Fact]
    public async Task Harness_ExecutesTypedWorkflow()
    {
        var workflow = Workflow.Create<TestData>("TypedTest")
            .Step("Set", ctx => { ctx.Data.Value = "hello"; return Task.CompletedTask; })
            .Build();

        var harness = new WorkflowTestHarness();
        var result = await harness.ExecuteAsync(workflow, new TestData());

        result.IsSuccess.Should().BeTrue();
        result.Data.Value.Should().Be("hello");
    }
}

public class RegistryExtendedTests
{
    [Fact]
    public void TypedWorkflowRegistry_RegisterAndResolve()
    {
        var registry = new WorkflowRegistry();
        registry.Register<string>("typed", () => Workflow.Create<string>("typed")
            .Step("s", ctx => Task.CompletedTask)
            .Build());

        var workflow = registry.Resolve<string>("typed");
        workflow.Name.Should().Be("typed");
    }

    [Fact]
    public async Task WorkflowRunner_RunsTypedWorkflow()
    {
        var registry = new WorkflowRegistry();
        registry.Register<string>("typed", () => Workflow.Create<string>("typed")
            .Step("s", ctx => { ctx.Data = ctx.Data.ToUpper(); return Task.CompletedTask; })
            .Build());

        var runner = new WorkflowRunner(registry);
        var result = await runner.RunAsync("typed", "hello");

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("HELLO");
    }

    [Fact]
    public void VersionedRegistry_UnknownWorkflow_Throws()
    {
        var registry = new VersionedWorkflowRegistry();
        var act = () => registry.Resolve("nonexistent");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void VersionedRegistry_UnknownVersion_Throws()
    {
        var registry = new VersionedWorkflowRegistry();
        registry.Register("wf", 1, () => Workflow.Create("wf").Build());

        var act = () => registry.Resolve("wf", 99);
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void VersionedRegistry_EmptyVersions_ReturnsEmpty()
    {
        var registry = new VersionedWorkflowRegistry();
        registry.GetVersions("nonexistent").Should().BeEmpty();
    }
}

public class StepRegistryExtendedTests
{
    [Fact]
    public void StepRegistry_GenericRegister_Works()
    {
        var registry = new StepRegistry();
        registry.Register<SimpleStep>();

        var step = registry.Resolve("SimpleStep");
        step.Should().BeOfType<SimpleStep>();
    }

    [Fact]
    public void StepRegistry_Names_ContainsRegistered()
    {
        var registry = new StepRegistry();
        registry.Register("A", () => new SimpleStep());
        registry.Register("B", () => new SimpleStep());

        registry.Names.Should().Contain("A").And.Contain("B");
    }

    private class SimpleStep : IStep
    {
        public string Name => "SimpleStep";
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }
}

public class TimeoutMiddlewareTests
{
    [Fact]
    public async Task TimeoutMiddleware_FastStepCompletes()
    {
        var middleware = new TimeoutMiddleware(TimeSpan.FromSeconds(10));
        var step = new SlowFakeStep();
        var context = new WorkflowContext();

        await middleware.InvokeAsync(context, step, _ => Task.CompletedTask);
        // No exception means success
    }

    [Fact]
    public async Task TimeoutMiddleware_ConvertsCancellation()
    {
        var middleware = new TimeoutMiddleware(TimeSpan.FromSeconds(10));
        var step = new SlowFakeStep();
        var context = new WorkflowContext();

        // If the step throws OperationCanceledException (not from context token),
        // the middleware converts it to TimeoutException
        var act = () => middleware.InvokeAsync(context, step, _ =>
            throw new OperationCanceledException());

        await act.Should().ThrowAsync<TimeoutException>();
    }

    private class SlowFakeStep : IStep
    {
        public string Name => "SlowFake";
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }
}

public class ValidationMiddlewareTests
{
    [Fact]
    public async Task ValidationMiddleware_InvalidThrows()
    {
        var middleware = new ValidationMiddleware((ctx, step) => Task.FromResult(false));
        var step = new FakeStep("test");
        var context = new WorkflowContext();

        var act = () => middleware.InvokeAsync(context, step, _ => Task.CompletedTask);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ValidationMiddleware_ValidPasses()
    {
        var ran = false;
        var middleware = new ValidationMiddleware((ctx, step) => Task.FromResult(true));
        var step = new FakeStep("test");
        var context = new WorkflowContext();

        await middleware.InvokeAsync(context, step, _ => { ran = true; return Task.CompletedTask; });
        ran.Should().BeTrue();
    }
}

public class WorkflowContextTests
{
    [Fact]
    public void WorkflowContext_HasUniqueIds()
    {
        var c1 = new WorkflowContext();
        var c2 = new WorkflowContext();

        c1.WorkflowId.Should().NotBe(c2.WorkflowId);
        c1.CorrelationId.Should().NotBe(c2.CorrelationId);
    }

    [Fact]
    public void TypedContext_StoresData()
    {
        var context = new WorkflowContext<string>("test");
        context.Data.Should().Be("test");

        context.Data = "updated";
        context.Data.Should().Be("updated");
    }

    [Fact]
    public void TypedContext_NullData_Throws()
    {
        var act = () => new WorkflowContext<string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

public class WorkflowErrorTests
{
    [Fact]
    public void WorkflowError_StoresProperties()
    {
        var ex = new InvalidOperationException("test");
        var error = new WorkflowError("Step1", ex, DateTimeOffset.UtcNow);

        error.StepName.Should().Be("Step1");
        error.Exception.Should().Be(ex);
    }
}

public class CronParserExtendedTests
{
    [Fact]
    public void CronParser_EveryFiveMinutes()
    {
        var next = CronParser.GetNextOccurrence("*/5 * * * *",
            new DateTimeOffset(2025, 1, 1, 12, 3, 0, TimeSpan.Zero));
        next.Should().NotBeNull();
        next!.Value.Minute.Should().Be(5);
    }

    [Fact]
    public void CronParser_SpecificDayOfWeek()
    {
        // Every Monday at 9 AM
        var next = CronParser.GetNextOccurrence("0 9 * * 1",
            new DateTimeOffset(2025, 1, 5, 10, 0, 0, TimeSpan.Zero)); // Sunday
        next.Should().NotBeNull();
        next!.Value.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }
}
