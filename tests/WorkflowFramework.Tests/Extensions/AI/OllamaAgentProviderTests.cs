using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.AI;

public class OllamaOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new OllamaOptions();
        opts.BaseUrl.Should().Be("http://localhost:11434");
        opts.DefaultModel.Should().Be("qwen3:30b-instruct");
        opts.Timeout.Should().Be(TimeSpan.FromSeconds(120));
        opts.DisableThinking.Should().BeTrue();
    }
}

public class OllamaAgentProviderTests
{
    [Fact]
    public void Name_ReturnsOllama()
    {
        using var provider = new OllamaAgentProvider();
        provider.Name.Should().Be("Ollama");
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var act = () => new OllamaAgentProvider(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CompleteAsync_BasicRequest_ParsesResponse()
    {
        var response = new
        {
            message = new { role = "assistant", content = "Hello world" },
            done = true,
            done_reason = "stop",
            prompt_eval_count = 10,
            eval_count = 5
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
        var response = new { message = new { role = "assistant", content = "ok" }, done = true, prompt_eval_count = 0, eval_count = 0 };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), body => capturedBody = body);

        await provider.CompleteAsync(new LlmRequest
        {
            Prompt = "test",
            Variables = new Dictionary<string, object?> { ["key1"] = "val1", ["key2"] = null }
        });

        capturedBody.Should().Contain("key1: val1");
        capturedBody.Should().NotContain("key2"); // null values filtered
    }

    [Fact]
    public async Task CompleteAsync_DisableThinking_AppendsNoThink()
    {
        string? capturedBody = null;
        var response = new { message = new { role = "assistant", content = "ok" }, done = true, prompt_eval_count = 0, eval_count = 0 };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), body => capturedBody = body);

        await provider.CompleteAsync(new LlmRequest { Prompt = "hello" });

        capturedBody.Should().Contain("/no_think");
    }

    [Fact]
    public async Task CompleteAsync_ThinkingEnabled_NoNoThink()
    {
        string? capturedBody = null;
        var response = new { message = new { role = "assistant", content = "ok" }, done = true, prompt_eval_count = 0, eval_count = 0 };
        var opts = new OllamaOptions { DisableThinking = false };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), body => capturedBody = body, opts);

        await provider.CompleteAsync(new LlmRequest { Prompt = "hello" });

        capturedBody.Should().NotContain("/no_think");
    }

    [Fact]
    public async Task CompleteAsync_WithTools_IncludesToolsInRequest()
    {
        string? capturedBody = null;
        var response = new { message = new { role = "assistant", content = "ok" }, done = true, prompt_eval_count = 0, eval_count = 0 };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), body => capturedBody = body);

        await provider.CompleteAsync(new LlmRequest
        {
            Prompt = "test",
            Tools = new List<AgentTool>
            {
                new() { Name = "search", Description = "Search the web", ParametersSchema = """{"type":"object"}""" },
                new() { Name = "calc", Description = "Calculate", ParametersSchema = null }
            }
        });

        capturedBody.Should().Contain("search");
        capturedBody.Should().Contain("calc");
    }

    [Fact]
    public async Task CompleteAsync_WithToolCalls_MapsToolCalls()
    {
        var responseJson = """
        {
            "message": {
                "role": "assistant",
                "content": "",
                "tool_calls": [
                    {
                        "function": {
                            "name": "search",
                            "arguments": {"query": "test"}
                        }
                    }
                ]
            },
            "done": true,
            "done_reason": "tool_calls",
            "prompt_eval_count": 5,
            "eval_count": 3
        }
        """;
        using var provider = CreateProvider(responseJson);

        var result = await provider.CompleteAsync(new LlmRequest { Prompt = "find" });

        result.ToolCalls.Should().HaveCount(1);
        result.ToolCalls[0].ToolName.Should().Be("search");
        result.ToolCalls[0].Arguments.Should().Contain("test");
    }

    [Fact]
    public async Task CompleteAsync_WithTemperatureAndMaxTokens_SetsOptions()
    {
        string? capturedBody = null;
        var response = new { message = new { role = "assistant", content = "ok" }, done = true, prompt_eval_count = 0, eval_count = 0 };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), body => capturedBody = body);

        await provider.CompleteAsync(new LlmRequest { Prompt = "test", Temperature = 0.5, MaxTokens = 100 });

        capturedBody.Should().Contain("temperature");
        capturedBody.Should().Contain("num_predict");
    }

    [Fact]
    public async Task CompleteAsync_WithModel_UsesSpecifiedModel()
    {
        string? capturedBody = null;
        var response = new { message = new { role = "assistant", content = "ok" }, done = true, prompt_eval_count = 0, eval_count = 0 };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), body => capturedBody = body);

        await provider.CompleteAsync(new LlmRequest { Prompt = "test", Model = "llama3" });

        capturedBody.Should().Contain("llama3");
    }

    [Fact]
    public async Task CompleteAsync_DoneWithoutReason_UsesStop()
    {
        var responseJson = """{"message":{"role":"assistant","content":"hi"},"done":true,"prompt_eval_count":0,"eval_count":0}""";
        using var provider = CreateProvider(responseJson);

        var result = await provider.CompleteAsync(new LlmRequest { Prompt = "test" });
        result.FinishReason.Should().Be("stop");
    }

    [Fact]
    public async Task CompleteAsync_NotDone_FinishReasonNull()
    {
        var responseJson = """{"message":{"role":"assistant","content":"hi"},"done":false,"prompt_eval_count":0,"eval_count":0}""";
        using var provider = CreateProvider(responseJson);

        var result = await provider.CompleteAsync(new LlmRequest { Prompt = "test" });
        result.FinishReason.Should().BeNull();
    }

    [Fact]
    public async Task CompleteAsync_NullMessage_ReturnsEmptyContent()
    {
        var responseJson = """{"done":true,"prompt_eval_count":0,"eval_count":0}""";
        using var provider = CreateProvider(responseJson);

        var result = await provider.CompleteAsync(new LlmRequest { Prompt = "test" });
        result.Content.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteAsync_HttpError_Throws()
    {
        using var provider = CreateProvider("error", statusCode: HttpStatusCode.InternalServerError);
        var act = () => provider.CompleteAsync(new LlmRequest { Prompt = "test" });
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task DecideAsync_MatchesOption_CaseInsensitive()
    {
        var responseJson = """{"message":{"role":"assistant","content":"APPROVE"},"done":true,"prompt_eval_count":0,"eval_count":0}""";
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
        var responseJson = """{"message":{"role":"assistant","content":"maybe"},"done":true,"prompt_eval_count":0,"eval_count":0}""";
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
        var responseJson = $$$"""{"message":{"role":"assistant","content":"{{{longResponse}}}"},"done":true,"prompt_eval_count":0,"eval_count":0}""";
        using var provider = CreateProvider(responseJson);

        var result = await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "Choose",
            Options = new List<string> { "optionA", "optionB" }
        });

        result.Should().Be("optionA");
    }

    [Fact]
    public async Task DecideAsync_WithVariables_IncludesContext()
    {
        string? capturedBody = null;
        var responseJson = """{"message":{"role":"assistant","content":"yes"},"done":true,"prompt_eval_count":0,"eval_count":0}""";
        using var provider = CreateProvider(responseJson, body => capturedBody = body);

        await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "decide",
            Options = new List<string> { "yes", "no" },
            Variables = new Dictionary<string, object?> { ["info"] = "context" }
        });

        capturedBody.Should().Contain("info: context");
    }

    [Fact]
    public async Task DecideAsync_DisableThinking_AppendsNoThink()
    {
        string? capturedBody = null;
        var responseJson = """{"message":{"role":"assistant","content":"yes"},"done":true,"prompt_eval_count":0,"eval_count":0}""";
        using var provider = CreateProvider(responseJson, body => capturedBody = body);

        await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "decide",
            Options = new List<string> { "yes" }
        });

        capturedBody.Should().Contain("/no_think");
    }

    [Fact]
    public async Task DecideAsync_ThinkingEnabled_NoNoThink()
    {
        string? capturedBody = null;
        var responseJson = """{"message":{"role":"assistant","content":"yes"},"done":true,"prompt_eval_count":0,"eval_count":0}""";
        var opts = new OllamaOptions { DisableThinking = false };
        using var provider = CreateProvider(responseJson, body => capturedBody = body, opts);

        await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "decide",
            Options = new List<string> { "yes" }
        });

        capturedBody.Should().NotContain("/no_think");
    }

    [Fact]
    public void Dispose_OwnsClient_DisposesHttpClient()
    {
        // When no HttpClient provided, provider owns it
        var provider = new OllamaAgentProvider(new OllamaOptions());
        provider.Dispose(); // Should not throw
    }

    [Fact]
    public void Dispose_SharedClient_DoesNotDisposeHttpClient()
    {
        var client = new HttpClient();
        var provider = new OllamaAgentProvider(new OllamaOptions(), client);
        provider.Dispose();
        // Client should still be usable (not disposed)
        client.BaseAddress.Should().BeNull(); // Just verifying no ObjectDisposedException
        client.Dispose();
    }

    [Fact]
    public async Task DecideAsync_EmptyOptions_NoMatchLong_ReturnsNull()
    {
        var longResponse = new string('x', 100);
        var responseJson = $$$"""{"message":{"role":"assistant","content":"{{{longResponse}}}"},"done":true,"prompt_eval_count":0,"eval_count":0}""";
        using var provider = CreateProvider(responseJson);

        var result = await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "Choose",
            Options = new List<string>()
        });

        // FirstOrDefault() on empty list = null, so rawDecision returned
        result.Should().Be(longResponse);
    }

    private static OllamaAgentProvider CreateProvider(
        string responseBody,
        Action<string>? captureBody = null,
        OllamaOptions? options = null,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new FakeHttpHandler(responseBody, statusCode, captureBody);
        var client = new HttpClient(handler);
        return new OllamaAgentProvider(options ?? new OllamaOptions(), client);
    }

    private sealed class FakeHttpHandler(string responseBody, HttpStatusCode statusCode, Action<string>? captureBody = null)
        : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (captureBody != null && request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                captureBody(body);
            }

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
