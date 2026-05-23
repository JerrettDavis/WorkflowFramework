using FluentAssertions;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Agents;

[Feature("DslEmitterStep — AI-driven DSL workflow step emission")]
public class DslEmitterStepScenarios : TinyBddTestBase
{
    public DslEmitterStepScenarios(ITestOutputHelper output) : base(output) { }

    // ── helpers ──────────────────────────────────────────────────────────

    private static IAgentProvider MakeProvider(string content)
    {
        var p = Substitute.For<IAgentProvider>();
        p.Name.Returns("mock");
        p.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = content, ToolCalls = new List<ToolCall>() });
        return p;
    }

    // ── constructor guards ────────────────────────────────────────────────

    [Scenario("Null provider throws ArgumentNullException"), Fact]
    public async Task NullProvider_Throws()
    {
        Exception? caught = null;
        try { _ = new DslEmitterStep(null!, new DslEmitterOptions()); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("constructing DslEmitterStep with null provider", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull();
                ((ArgumentNullException)ex!).ParamName.Should().Be("provider");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null options throws ArgumentNullException"), Fact]
    public async Task NullOptions_Throws()
    {
        var provider = MakeProvider("[]");
        Exception? caught = null;
        try { _ = new DslEmitterStep(provider, null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("constructing DslEmitterStep with null options", () => caught)
            .Then("ArgumentNullException is thrown with paramName options", ex =>
            {
                ex.Should().NotBeNull();
                ((ArgumentNullException)ex!).ParamName.Should().Be("options");
                return true;
            })
            .AssertPassed();
    }

    // ── Name property ─────────────────────────────────────────────────────

    [Scenario("Step name defaults to 'DslEmitter' when StepName is null"), Fact]
    public async Task Name_DefaultsWhenStepNameNull()
    {
        var step = new DslEmitterStep(MakeProvider("[]"), new DslEmitterOptions());

        await Given("a DslEmitterStep with default options", () => step)
            .Then("the step name is 'DslEmitter'", s =>
            {
                s.Name.Should().Be("DslEmitter");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Step name uses StepName when set"), Fact]
    public async Task Name_UsesStepNameWhenSet()
    {
        var step = new DslEmitterStep(MakeProvider("[]"), new DslEmitterOptions { StepName = "MyEmitter" });

        await Given("a DslEmitterStep with custom StepName", () => step)
            .Then("the step name matches StepName", s =>
            {
                s.Name.Should().Be("MyEmitter");
                return true;
            })
            .AssertPassed();
    }

    // ── happy-path execution ──────────────────────────────────────────────

    [Scenario("Valid JSON array response is stored in EmittedSteps property"), Fact]
    public async Task ValidJsonArrayResponse_StoredAsEmittedSteps()
    {
        var jsonResponse = "[{\"step\":\"build\",\"cmd\":\"dotnet build\"},{\"step\":\"test\"}]";
        var step = new DslEmitterStep(MakeProvider(jsonResponse), new DslEmitterOptions { MaxIterations = 1 });
        var context = new WorkflowContext();

        await step.ExecuteAsync(context);

        await Given("context after executing DslEmitterStep with valid JSON array response", () => context)
            .Then("EmittedSteps property contains two items", ctx =>
            {
                var key = step.Name + ".EmittedSteps";
                ctx.Properties.Should().ContainKey(key);
                ((List<object?>)ctx.Properties[key]!).Should().HaveCount(2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Task property in context is used as user prompt"), Fact]
    public async Task TaskProperty_UsedAsUserPrompt()
    {
        LlmRequest? capturedRequest = null;
        var provider = Substitute.For<IAgentProvider>();
        provider.Name.Returns("mock");
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedRequest = ci.Arg<LlmRequest>();
                return Task.FromResult(new LlmResponse { Content = "[]", ToolCalls = new List<ToolCall>() });
            });

        var step = new DslEmitterStep(provider, new DslEmitterOptions { MaxIterations = 1 });
        var context = new WorkflowContext();
        context.Properties["task"] = "generate CI/CD pipeline";

        await step.ExecuteAsync(context);

        await Given("context with task property", () => capturedRequest)
            .Then("the task text was included in the request prompt", req =>
            {
                req!.Prompt.Should().Contain("generate CI/CD pipeline");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Iterations count is stored after execution"), Fact]
    public async Task IterationsCount_Stored()
    {
        var step = new DslEmitterStep(MakeProvider("[{}]"), new DslEmitterOptions { MaxIterations = 3 });
        var context = new WorkflowContext();

        await step.ExecuteAsync(context);

        await Given("context after executing DslEmitterStep", () => context)
            .Then("Iterations property is set and at least 1", ctx =>
            {
                var key = step.Name + ".Iterations";
                ctx.Properties.Should().ContainKey(key);
                ((int)ctx.Properties[key]!).Should().BeGreaterThanOrEqualTo(1);
                return true;
            })
            .AssertPassed();
    }

    // ── retry behavior ────────────────────────────────────────────────────

    [Scenario("Non-JSON response causes retry up to MaxIterations"), Fact]
    public async Task NonJsonResponse_CausesRetryUpToMaxIterations()
    {
        // Provider always returns prose — no valid JSON array
        var step = new DslEmitterStep(MakeProvider("Here are the steps you need to follow:"),
            new DslEmitterOptions { MaxIterations = 2 });
        var context = new WorkflowContext();

        await step.ExecuteAsync(context);

        await Given("context after DslEmitter with non-parseable response", () => context)
            .Then("EmittedSteps is empty and Iterations equals MaxIterations", ctx =>
            {
                var emittedKey = step.Name + ".EmittedSteps";
                var iterKey = step.Name + ".Iterations";
                ((List<object?>)ctx.Properties[emittedKey]!).Should().BeEmpty();
                ((int)ctx.Properties[iterKey]!).Should().Be(2);
                return true;
            })
            .AssertPassed();
    }

    // ── cancellation ──────────────────────────────────────────────────────

    [Scenario("Cancellation before first LLM call aborts cleanly"), Fact]
    public async Task CancellationDuringLlmCall_AbortsCleanly()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.Name.Returns("mock");
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<LlmResponse>>(ci =>
            {
                ci.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return Task.FromResult(new LlmResponse { Content = "[]", ToolCalls = new List<ToolCall>() });
            });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var step = new DslEmitterStep(provider, new DslEmitterOptions { MaxIterations = 3 });
        var context = new WorkflowContext(cts.Token);

        // Should not throw — OperationCanceledException is caught internally
        await step.ExecuteAsync(context);

        await Given("context after cancelled DslEmitterStep execution", () => context)
            .Then("EmittedSteps is empty", ctx =>
            {
                ((List<object?>)ctx.Properties[step.Name + ".EmittedSteps"]!).Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    // ── DslEmitterOptions ────────────────────────────────────────────────

    [Scenario("DslEmitterOptions MaxIterations defaults to 3"), Fact]
    public async Task DslEmitterOptions_MaxIterationsDefaults()
    {
        var options = new DslEmitterOptions();

        await Given("default DslEmitterOptions", () => options)
            .Then("MaxIterations is 3 and StepName is null", o =>
            {
                o.MaxIterations.Should().Be(3);
                o.StepName.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("JSON array embedded in prose is extracted"), Fact]
    public async Task JsonArrayEmbeddedInProse_IsExtracted()
    {
        // Response has prose before and after the array
        var response = "Sure! Here is the plan: [{\"step\":\"build\"}] Hope that helps!";
        var step = new DslEmitterStep(MakeProvider(response), new DslEmitterOptions { MaxIterations = 1 });
        var context = new WorkflowContext();

        await step.ExecuteAsync(context);

        await Given("context after executing with array embedded in prose", () => context)
            .Then("EmittedSteps has one item extracted", ctx =>
            {
                var emitted = (List<object?>)ctx.Properties[step.Name + ".EmittedSteps"]!;
                emitted.Should().HaveCount(1);
                return true;
            })
            .AssertPassed();
    }
}
