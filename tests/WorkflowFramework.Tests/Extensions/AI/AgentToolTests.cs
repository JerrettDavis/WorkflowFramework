using FluentAssertions;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.AI;

public class AgentToolTests
{
    [Fact]
    public void AgentTool_Defaults()
    {
        var t = new AgentTool();
        t.Name.Should().BeEmpty();
        t.Description.Should().BeEmpty();
        t.ParametersSchema.Should().BeNull();
    }

    [Fact]
    public void AgentTool_SetProperties()
    {
        var t = new AgentTool { Name = "search", Description = "Search", ParametersSchema = "{}" };
        t.Name.Should().Be("search");
        t.ParametersSchema.Should().Be("{}");
    }

    [Fact]
    public void ToolCall_Defaults()
    {
        var tc = new ToolCall();
        tc.ToolName.Should().BeEmpty();
        tc.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void ToolCall_SetProperties()
    {
        var tc = new ToolCall { ToolName = "fn", Arguments = "{\"q\":\"x\"}" };
        tc.ToolName.Should().Be("fn");
    }

    [Fact]
    public void TokenUsage_Properties()
    {
        var u = new TokenUsage { PromptTokens = 10, CompletionTokens = 20, TotalTokens = 30 };
        u.TotalTokens.Should().Be(30);
    }

    [Fact]
    public void LlmResponse_Defaults()
    {
        var r = new LlmResponse();
        r.Content.Should().BeEmpty();
        r.ToolCalls.Should().BeEmpty();
        r.FinishReason.Should().BeNull();
        r.Usage.Should().BeNull();
    }

    [Fact]
    public void LlmRequest_Defaults()
    {
        var r = new LlmRequest();
        r.Prompt.Should().BeEmpty();
        r.Variables.Should().BeEmpty();
        r.Model.Should().BeNull();
        r.Temperature.Should().BeNull();
        r.MaxTokens.Should().BeNull();
        r.Tools.Should().BeEmpty();
    }

    [Fact]
    public void AgentDecisionRequest_Defaults()
    {
        var r = new AgentDecisionRequest();
        r.Prompt.Should().BeEmpty();
        r.Options.Should().BeEmpty();
        r.Variables.Should().BeEmpty();
    }
}
