using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.AI;

public class AnthropicOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new AnthropicOptions();
        opts.DefaultModel.Should().Be("claude-sonnet-4-20250514");
        opts.BaseUrl.Should().Be("https://api.anthropic.com");
        opts.Timeout.Should().Be(TimeSpan.FromSeconds(120));
        opts.ApiKey.Should().BeEmpty();
        opts.MaxTokens.Should().Be(4096);
        opts.AnthropicVersion.Should().Be("2023-06-01");
    }
}

public class AnthropicAgentProviderTests
{
    [Fact]
    public void Name_ReturnsAnthropic()
    {
        using var provider = new AnthropicAgentProvider(new AnthropicOptions());
        provider.Name.Should().Be("Anthropic");
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var act = () => new AnthropicAgentProvider(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CompleteAsync_BasicRequest_ParsesResponse()
    {
        var response = new
        {
            content = new[] { new { type = "text", text = "Hello world" } },
            stop_reason = "end_turn",
            usage = new { input_tokens = 10, output_tokens = 5 }
        };
        using var provider = CreateProvider(JsonSerializer.Serialize(response));

        var result = await provider.CompleteAsync(new LlmRequest { Prompt = "Hi" });

        result.Content.Should().Be("Hello world");
        result.FinishReason.Should().Be("end_turn");
        result.Usage!.PromptTokens.Should().Be(10);
        result.Usage.CompletionTokens.Should().Be(5);
        result.Usage.TotalTokens.Should().Be(15);
    }

    [Fact]
    public async Task CompleteAsync_WithVariables_AddsSystemParam()
    {
        string? capturedBody = null;
        var response = new { content = new[] { new { type = "text", text = "ok" } }, stop_reason = "end_turn", usage = new { input_tokens = 0, output_tokens = 0 } };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), captureBody: body => capturedBody = body);

        await provider.CompleteAsync(new LlmRequest
        {
            Prompt = "test",
            Variables = new Dictionary<string, object?> { ["key1"] = "val1" }
        });

        capturedBody.Should().Contain("key1: val1");
        capturedBody.Should().Contain("system");
    }

    [Fact]
    public async Task CompleteAsync_WithToolUse_ParsesToolCalls()
    {
        var responseJson = """
        {
            "content": [
                { "type": "text", "text": "Let me search." },
                { "type": "tool_use", "id": "tu_1", "name": "search", "input": {"query": "test"} }
            ],
            "stop_reason": "tool_use",
            "usage": { "input_tokens": 5, "output_tokens": 3 }
        }
        """;
        using var provider = CreateProvider(responseJson);

        var result = await provider.CompleteAsync(new LlmRequest { Prompt = "find" });

        result.Content.Should().Be("Let me search.");
        result.ToolCalls.Should().HaveCount(1);
        result.ToolCalls[0].ToolName.Should().Be("search");
        result.ToolCalls[0].Arguments.Should().Contain("test");
    }

    [Fact]
    public async Task CompleteAsync_SendsCorrectHeaders()
    {
        Dictionary<string, string>? capturedHeaders = null;
        var response = new { content = new[] { new { type = "text", text = "ok" } }, stop_reason = "end_turn", usage = new { input_tokens = 0, output_tokens = 0 } };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), captureHeaders: h => capturedHeaders = h);

        await provider.CompleteAsync(new LlmRequest { Prompt = "test" });

        capturedHeaders.Should().ContainKey("x-api-key").WhoseValue.Should().Be("test-key");
        capturedHeaders.Should().ContainKey("anthropic-version").WhoseValue.Should().Be("2023-06-01");
    }

    [Fact]
    public async Task CompleteAsync_WithTools_IncludesToolsInRequest()
    {
        string? capturedBody = null;
        var response = new { content = new[] { new { type = "text", text = "ok" } }, stop_reason = "end_turn", usage = new { input_tokens = 0, output_tokens = 0 } };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), captureBody: body => capturedBody = body);

        await provider.CompleteAsync(new LlmRequest
        {
            Prompt = "test",
            Tools = new List<AgentTool>
            {
                new() { Name = "search", Description = "Search the web", ParametersSchema = """{"type":"object"}""" }
            }
        });

        capturedBody.Should().Contain("search");
        capturedBody.Should().Contain("input_schema");
    }

    [Fact]
    public async Task DecideAsync_MatchesOption_CaseInsensitive()
    {
        var responseJson = """{"content":[{"type":"text","text":"APPROVE"}],"stop_reason":"end_turn","usage":{"input_tokens":0,"output_tokens":0}}""";
        using var provider = CreateProvider(responseJson);

        var result = await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "Should we approve?",
            Options = new List<string> { "approve", "reject" }
        });

        result.Should().Be("approve");
    }

    [Fact]
    public async Task DecideAsync_NoMatch_ShortResponse_ReturnsRaw()
    {
        var responseJson = """{"content":[{"type":"text","text":"maybe"}],"stop_reason":"end_turn","usage":{"input_tokens":0,"output_tokens":0}}""";
        using var provider = CreateProvider(responseJson);

        var result = await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "Choose",
            Options = new List<string> { "yes", "no" }
        });

        result.Should().Be("maybe");
    }

    [Fact]
    public async Task DecideAsync_NoMatch_LongResponse_ReturnsFirstOption()
    {
        var longResponse = new string('x', 100);
        var responseJson = $$$"""{"content":[{"type":"text","text":"{{{longResponse}}}"}],"stop_reason":"end_turn","usage":{"input_tokens":0,"output_tokens":0}}""";
        using var provider = CreateProvider(responseJson);

        var result = await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "Choose",
            Options = new List<string> { "optionA", "optionB" }
        });

        result.Should().Be("optionA");
    }

    [Fact]
    public async Task CompleteAsync_HttpError_Throws()
    {
        using var provider = CreateProvider("error", statusCode: HttpStatusCode.InternalServerError);
        var act = () => provider.CompleteAsync(new LlmRequest { Prompt = "test" });
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private static AnthropicAgentProvider CreateProvider(
        string responseBody,
        Action<string>? captureBody = null,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        Action<Dictionary<string, string>>? captureHeaders = null)
    {
        var handler = new FakeHttpHandler(responseBody, statusCode, captureBody, captureHeaders);
        var client = new HttpClient(handler);
        return new AnthropicAgentProvider(new AnthropicOptions { ApiKey = "test-key" }, client);
    }

    private sealed class FakeHttpHandler(string responseBody, HttpStatusCode statusCode, Action<string>? captureBody = null, Action<Dictionary<string, string>>? captureHeaders = null)
        : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (captureBody != null && request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                captureBody(body);
            }

            if (captureHeaders != null)
            {
                var headers = new Dictionary<string, string>();
                foreach (var h in request.Headers)
                {
                    headers[h.Key] = string.Join(",", h.Value);
                }
                captureHeaders(headers);
            }

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
