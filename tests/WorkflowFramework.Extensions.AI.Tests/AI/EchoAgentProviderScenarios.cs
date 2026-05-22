using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.AI.Tests.AI;

[Feature("EchoAgentProvider — testable no-op agent provider")]
public class EchoAgentProviderScenarios : TinyBddXunitBase
{
    public EchoAgentProviderScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("Name property returns 'echo'"), Fact]
    public async Task Name_ReturnsEcho()
    {
        var provider = new EchoAgentProvider();

        await Given("an EchoAgentProvider", () => provider.Name)
            .Then("name is 'echo'", name =>
            {
                name.Should().Be("echo");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CompleteAsync echoes the prompt in the Content field"), Fact]
    public async Task CompleteAsync_EchoesPromptInContent()
    {
        var provider = new EchoAgentProvider();
        var request = new LlmRequest { Prompt = "Hello, world!" };

        var response = await provider.CompleteAsync(request);

        await Given("a prompt 'Hello, world!'", () => response)
            .Then("Content contains 'Echo: Hello, world!'", r =>
            {
                r.Content.Should().Contain("Hello, world!");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CompleteAsync returns stop finish reason"), Fact]
    public async Task CompleteAsync_ReturnsStopFinishReason()
    {
        var provider = new EchoAgentProvider();
        var response = await provider.CompleteAsync(new LlmRequest { Prompt = "test" });

        await Given("any request", () => response)
            .Then("FinishReason is 'stop'", r =>
            {
                r.FinishReason.Should().Be("stop");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CompleteAsync populates token usage"), Fact]
    public async Task CompleteAsync_PopulatesUsage()
    {
        var provider = new EchoAgentProvider();
        var prompt = "abc"; // 3 chars
        var response = await provider.CompleteAsync(new LlmRequest { Prompt = prompt });

        await Given("a 3-character prompt", () => response.Usage)
            .Then("Usage is populated with non-zero token counts", u =>
            {
                u.Should().NotBeNull();
                u!.PromptTokens.Should().BeGreaterThan(0);
                u.TotalTokens.Should().BeGreaterThan(0);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("DecideAsync returns first option when options provided"), Fact]
    public async Task DecideAsync_ReturnsFirstOption()
    {
        var provider = new EchoAgentProvider();
        var request = new AgentDecisionRequest
        {
            Prompt = "Pick one",
            Options = new List<string> { "alpha", "beta", "gamma" }
        };

        var decision = await provider.DecideAsync(request);

        await Given("a decision request with 3 options", () => decision)
            .Then("returns the first option 'alpha'", d =>
            {
                d.Should().Be("alpha");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("DecideAsync returns 'default' when options list is empty"), Fact]
    public async Task DecideAsync_EmptyOptions_ReturnsDefault()
    {
        var provider = new EchoAgentProvider();
        var request = new AgentDecisionRequest
        {
            Prompt = "Pick one",
            Options = new List<string>()
        };

        var decision = await provider.DecideAsync(request);

        await Given("an empty options list", () => decision)
            .Then("returns 'default'", d =>
            {
                d.Should().Be("default");
                return true;
            })
            .AssertPassed();
    }
}
