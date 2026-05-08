using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.AI;

public class HuggingFaceOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new HuggingFaceOptions();
        opts.DefaultModel.Should().Be("mistralai/Mistral-7B-Instruct-v0.3");
        opts.BaseUrl.Should().Be("https://api-inference.huggingface.co");
        opts.Timeout.Should().Be(TimeSpan.FromSeconds(120));
        opts.ApiKey.Should().BeEmpty();
        opts.Temperature.Should().BeNull();
        opts.MaxTokens.Should().BeNull();
    }
}

public class HuggingFaceAgentProviderTests
{
    [Fact]
    public void Name_ReturnsHuggingFace()
    {
        using var provider = new HuggingFaceAgentProvider(new HuggingFaceOptions());
        provider.Name.Should().Be("HuggingFace");
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var act = () => new HuggingFaceAgentProvider(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CompleteAsync_BasicRequest_ParsesResponse()
    {
        var response = new[] { new { generated_text = "Hello world" } };
        using var provider = CreateProvider(JsonSerializer.Serialize(response));

        var result = await provider.CompleteAsync(new LlmRequest { Prompt = "Hi" });

        result.Content.Should().Be("Hello world");
        result.FinishReason.Should().Be("stop");
    }

    [Fact]
    public async Task CompleteAsync_WithVariables_IncludesContext()
    {
        string? capturedBody = null;
        var response = new[] { new { generated_text = "ok" } };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), captureBody: body => capturedBody = body);

        await provider.CompleteAsync(new LlmRequest
        {
            Prompt = "test",
            Variables = new Dictionary<string, object?> { ["key1"] = "val1" }
        });

        capturedBody.Should().Contain("key1: val1");
    }

    [Fact]
    public async Task CompleteAsync_SetsAuthorizationHeader()
    {
        string? capturedAuth = null;
        var response = new[] { new { generated_text = "ok" } };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), captureAuth: h => capturedAuth = h);

        await provider.CompleteAsync(new LlmRequest { Prompt = "test" });

        capturedAuth.Should().Be("Bearer test-key");
    }

    [Fact]
    public async Task CompleteAsync_WithTools_IncludesToolDescriptionsAndParsesToolCall()
    {
        string? capturedBody = null;
        var response = new[]
        {
            new
            {
                generated_text = """{"tool_name":"search","arguments":{"query":"workflow"}}"""
            }
        };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), captureBody: body => capturedBody = body);

        var result = await provider.CompleteAsync(new LlmRequest
        {
            Prompt = "Find workflow docs",
            Tools =
            {
                new AgentTool
                {
                    Name = "search",
                    Description = "Search documentation",
                    ParametersSchema = """{"type":"object"}"""
                }
            }
        });

        capturedBody.Should().Contain("Available tools:");
        capturedBody.Should().Contain("search");
        capturedBody.Should().Contain("Search documentation");
        result.ToolCalls.Should().ContainSingle();
        result.ToolCalls[0].ToolName.Should().Be("search");
        result.ToolCalls[0].Arguments.Should().Contain("workflow");
    }

    [Fact]
    public async Task CompleteAsync_InvalidToolCallJson_IgnoresToolCall()
    {
        var response = new[] { new { generated_text = "{not-valid-json}" } };
        using var provider = CreateProvider(JsonSerializer.Serialize(response));

        var result = await provider.CompleteAsync(new LlmRequest { Prompt = "test" });

        result.Content.Should().Be("{not-valid-json}");
        result.ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteAsync_UsesRequestModelAndGenerationOverrides()
    {
        string? capturedBody = null;
        using var provider = CreateProvider(
            JsonSerializer.Serialize(new[] { new { generated_text = "ok" } }),
            captureBody: body => capturedBody = body);

        await provider.CompleteAsync(new LlmRequest
        {
            Prompt = "test",
            Model = "custom-model",
            Temperature = 0.7,
            MaxTokens = 42
        });

        capturedBody.Should().Contain("\"temperature\":0.7");
        capturedBody.Should().Contain("\"max_new_tokens\":42");
        capturedBody.Should().Contain("\"inputs\":\"test\"");
    }

    [Fact]
    public async Task DecideAsync_MatchesOption_CaseInsensitive()
    {
        var response = new[] { new { generated_text = "APPROVE" } };
        using var provider = CreateProvider(JsonSerializer.Serialize(response));

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
        var response = new[] { new { generated_text = "maybe" } };
        using var provider = CreateProvider(JsonSerializer.Serialize(response));

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
        var response = new[] { new { generated_text = longResponse } };
        using var provider = CreateProvider(JsonSerializer.Serialize(response));

        var result = await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "Choose",
            Options = new List<string> { "optionA", "optionB" }
        });

        result.Should().Be("optionA");
    }

    [Fact]
    public async Task DecideAsync_WithVariables_IncludesContextAndSkipsNullValues()
    {
        string? capturedBody = null;
        var response = new[] { new { generated_text = "approve" } };
        using var provider = CreateProvider(JsonSerializer.Serialize(response), captureBody: body => capturedBody = body);

        await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "Choose",
            Options = new List<string> { "approve", "reject" },
            Variables = new Dictionary<string, object?>
            {
                ["priority"] = "high",
                ["ignored"] = null
            }
        });

        capturedBody.Should().Contain("Context:\\n- priority: high");
        capturedBody.Should().NotContain("ignored");
    }

    [Fact]
    public async Task CompleteAsync_HttpError_Throws()
    {
        using var provider = CreateProvider("error", statusCode: HttpStatusCode.InternalServerError);
        var act = () => provider.CompleteAsync(new LlmRequest { Prompt = "test" });
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Dispose_WithInjectedHttpClient_DoesNotDisposeSharedClient()
    {
        var handler = new FakeHttpHandler(
            JsonSerializer.Serialize(new[] { new { generated_text = "ok" } }),
            HttpStatusCode.OK);
        var client = new HttpClient(handler);
        var provider = new HuggingFaceAgentProvider(new HuggingFaceOptions { ApiKey = "test-key" }, client);

        provider.Dispose();

        var act = async () => await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test"));
        await act.Should().NotThrowAsync();
        client.Dispose();
    }

    private static HuggingFaceAgentProvider CreateProvider(
        string responseBody,
        Action<string>? captureBody = null,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        Action<string>? captureAuth = null)
    {
        var handler = new FakeHttpHandler(responseBody, statusCode, captureBody, captureAuth);
        var client = new HttpClient(handler);
        return new HuggingFaceAgentProvider(new HuggingFaceOptions { ApiKey = "test-key" }, client);
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
