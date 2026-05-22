using FluentAssertions;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.AI.Tests.AI;

[Feature("LlmCallStep — workflow step that calls an LLM provider")]
public class LlmCallStepScenarios : TinyBddXunitBase
{
    public LlmCallStepScenarios(ITestOutputHelper output) : base(output) { }

    private static IAgentProvider MakeEchoProvider() => new EchoAgentProvider();

    [Scenario("Default step name is 'LlmCall' when not configured"), Fact]
    public async Task DefaultStepName()
    {
        var step = new LlmCallStep(MakeEchoProvider(), new LlmCallOptions());

        await Given("LlmCallStep with no explicit name", () => step.Name)
            .Then("name is 'LlmCall'", name =>
            {
                name.Should().Be("LlmCall");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Custom step name from options is used"), Fact]
    public async Task CustomStepName()
    {
        var step = new LlmCallStep(MakeEchoProvider(), new LlmCallOptions { StepName = "MyLlmStep" });

        await Given("LlmCallStep with StepName='MyLlmStep'", () => step.Name)
            .Then("name is 'MyLlmStep'", name =>
            {
                name.Should().Be("MyLlmStep");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync stores response in context properties"), Fact]
    public async Task ExecuteAsync_StoresResponseInProperties()
    {
        var step = new LlmCallStep(MakeEchoProvider(), new LlmCallOptions { PromptTemplate = "Say hello" });
        var context = new WorkflowContext();

        await step.ExecuteAsync(context);

        await Given("LlmCallStep executed with echo provider", () => context.Properties)
            .Then("LlmCall.Response is populated in context properties", props =>
            {
                props.Should().ContainKey("LlmCall.Response");
                props["LlmCall.Response"].Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync stores FinishReason in context properties"), Fact]
    public async Task ExecuteAsync_StoresFinishReason()
    {
        var step = new LlmCallStep(MakeEchoProvider(), new LlmCallOptions { PromptTemplate = "test" });
        var context = new WorkflowContext();

        await step.ExecuteAsync(context);

        await Given("LlmCallStep executed", () => context.Properties)
            .Then("LlmCall.FinishReason is stored", props =>
            {
                props.Should().ContainKey("LlmCall.FinishReason");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync resolves prompt template tokens from context"), Fact]
    public async Task ExecuteAsync_ResolvesPromptTemplate()
    {
        var mockProvider = Substitute.For<IAgentProvider>();
        mockProvider.Name.Returns("mock");
        string? capturedPrompt = null;
        mockProvider.CompleteAsync(Arg.Do<LlmRequest>(r => capturedPrompt = r.Prompt), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "ok", FinishReason = "stop" });

        var step = new LlmCallStep(mockProvider, new LlmCallOptions { PromptTemplate = "User: {UserName}" });
        var context = new WorkflowContext();
        context.Properties["UserName"] = "Bob";

        await step.ExecuteAsync(context);

        await Given("template 'User: {UserName}' with UserName=Bob in context", () => capturedPrompt)
            .Then("rendered prompt sent to provider contains 'Bob'", p =>
            {
                p.Should().Contain("Bob");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null provider throws ArgumentNullException"), Fact]
    public async Task NullProvider_Throws()
    {
        Exception? caught = null;
        try { _ = new LlmCallStep(null!, new LlmCallOptions()); }
        catch (Exception ex) { caught = ex; }

        await Given("null provider passed to LlmCallStep constructor", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }
}
