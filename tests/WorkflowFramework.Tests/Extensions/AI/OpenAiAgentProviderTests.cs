using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.AI;

public class OpenAiOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new OpenAiOptions();
        opts.DefaultModel.Should().Be("gpt-4o");
        opts.BaseUrl.Should().Be("https://api.openai.com/v1");
        opts.Timeout.Should().Be(TimeSpan.FromSeconds(120));
        opts.ApiKey.Should().BeEmpty();
        opts.Temperature.Should().BeNull();
        opts.MaxTokens.Should().BeNull();
    }
}

public class OpenAiAgentProviderTests
{
    [Fact]
    public void Name_ReturnsOpenAI()
    {
        using var provider = new OpenAiAgentProvider(new OpenAiOptions());
        provider.Name.Should().Be("OpenAI");
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var act = () => new OpenAiAgentProvider(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CompleteAsync_BasicRequest_ParsesResponse()
    {
        var response = new
        {
            choices = new[]
            {
                new
                {
                    message = new { role = "assistant", content = "Hello world" },
                    finish_reason = "stop"
                }
            },
            usage = new { prompt_tokens = 10, completion_tokens = 5 }
        };
        using var provider = CreateProvider(JsonSerializer.Serialize(response));

        var result = await provider.CompleteAsync(new LlmRequest { Prompt = "Hi" });

        result.Content.Should().Be("Hello world");
        result.FinishReason.Should().Be("stop");
        result.Usage!.PromptTokens.Should().Be(10);
        result.Usage.CompletionTokens.Should().Be(5);
        result.Usage.TotalTokens.Should().Be(15);
    }

    [Fact]
    public async Task CompleteAsync_WithVariables_AddsSystemMessage()
    {
        string? capturedBody = null;
        var response = new { choices = new[] { new { message = new { role = "assistant", content = "ok" }, finish_reason = "stop" } }, usage = new { prompt_tokens = 0, completion_tokens = 0 } };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), body => capturedBody = body);

        await provider.CompleteAsync(new LlmRequest
        {
            Prompt = "test",
            Variables = new Dictionary<string, object?> { ["key1"] = "val1", ["key2"] = null }
        });

        capturedBody.Should().Contain("key1: val1");
        capturedBody.Should().NotContain("key2");
    }

    [Fact]
    public async Task CompleteAsync_WithTools_IncludesToolsInRequest()
    {
        string? capturedBody = null;
        var response = new { choices = new[] { new { message = new { role = "assistant", content = "ok" }, finish_reason = "stop" } }, usage = new { prompt_tokens = 0, completion_tokens = 0 } };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), body => capturedBody = body);

        await provider.CompleteAsync(new LlmRequest
        {
            Prompt = "test",
            Tools = new List<AgentTool>
            {
                new() { Name = "search", Description = "Search the web", ParametersSchema = """{"type":"object"}""" }
            }
        });

        capturedBody.Should().Contain("search");
        capturedBody.Should().Contain("function");
    }

    [Fact]
    public async Task CompleteAsync_WithToolCalls_MapsToolCalls()
    {
        var responseJson = """
        {
            "choices": [{
                "message": {
                    "role": "assistant",
                    "content": null,
                    "tool_calls": [{
                        "id": "call_123",
                        "type": "function",
                        "function": { "name": "search", "arguments": "{\"query\":\"test\"}" }
                    }]
                },
                "finish_reason": "tool_calls"
            }],
            "usage": { "prompt_tokens": 5, "completion_tokens": 3 }
        }
        """;
        using var provider = CreateProvider(responseJson);

        var result = await provider.CompleteAsync(new LlmRequest { Prompt = "find" });

        result.ToolCalls.Should().HaveCount(1);
        result.ToolCalls[0].ToolName.Should().Be("search");
        result.ToolCalls[0].Arguments.Should().Contain("test");
    }

    [Fact]
    public async Task CompleteAsync_SetsAuthorizationHeader()
    {
        string? capturedAuth = null;
        var response = new { choices = new[] { new { message = new { role = "assistant", content = "ok" }, finish_reason = "stop" } }, usage = new { prompt_tokens = 0, completion_tokens = 0 } };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), headers: h => capturedAuth = h);

        await provider.CompleteAsync(new LlmRequest { Prompt = "test" });

        capturedAuth.Should().Be("Bearer test-key");
    }

    [Fact]
    public async Task DecideAsync_MatchesOption_CaseInsensitive()
    {
        var responseJson = """{"choices":[{"message":{"role":"assistant","content":"APPROVE"},"finish_reason":"stop"}],"usage":{"prompt_tokens":0,"completion_tokens":0}}""";
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
        var responseJson = """{"choices":[{"message":{"role":"assistant","content":"maybe"},"finish_reason":"stop"}],"usage":{"prompt_tokens":0,"completion_tokens":0}}""";
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
        var responseJson = $$$"""{"choices":[{"message":{"role":"assistant","content":"{{{longResponse}}}"},"finish_reason":"stop"}],"usage":{"prompt_tokens":0,"completion_tokens":0}}""";
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

    private static OpenAiAgentProvider CreateProvider(
        string responseBody,
        Action<string>? captureBody = null,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        Action<string>? headers = null)
    {
        var handler = new FakeHttpHandler(responseBody, statusCode, captureBody, headers);
        var client = new HttpClient(handler);
        return new OpenAiAgentProvider(new OpenAiOptions { ApiKey = "test-key" }, client);
    }

    private sealed class FakeHttpHandler(string responseBody, HttpStatusCode statusCode, Action<string>? captureBody = null, Action<string>? captureAuth = null)
        : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (captureBody != null && request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                captureBody(body);
            }

            if (captureAuth != null && request.Headers.Authorization != null)
            {
                captureAuth($"{request.Headers.Authorization.Scheme} {request.Headers.Authorization.Parameter}");
            }

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
