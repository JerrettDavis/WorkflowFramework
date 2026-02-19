using FluentAssertions;
using WorkflowFramework.Extensions.Http;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Http;

public class AuthProviderTests
{
    [Fact]
    public void ApiKeyAuthProvider_NullApiKey_Throws()
    {
        FluentActions.Invoking(() => new ApiKeyAuthProvider(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ApiKeyAuthProvider_DefaultHeader()
    {
        var auth = new ApiKeyAuthProvider("key123");
        var req = new HttpRequestMessage();
        await auth.ApplyAsync(req);
        req.Headers.GetValues("X-Api-Key").Should().Contain("key123");
    }

    [Fact]
    public async Task ApiKeyAuthProvider_CustomHeader()
    {
        var auth = new ApiKeyAuthProvider("key", "Authorization");
        var req = new HttpRequestMessage();
        await auth.ApplyAsync(req);
        req.Headers.GetValues("Authorization").Should().Contain("key");
    }

    [Fact]
    public async Task BearerTokenAuthProvider_StaticToken()
    {
        var auth = new BearerTokenAuthProvider("tok");
        var req = new HttpRequestMessage();
        await auth.ApplyAsync(req);
        req.Headers.Authorization!.Scheme.Should().Be("Bearer");
        req.Headers.Authorization.Parameter.Should().Be("tok");
    }

    [Fact]
    public async Task BearerTokenAuthProvider_DynamicTokenFactory()
    {
        var auth = new BearerTokenAuthProvider(_ => Task.FromResult("dynamic-tok"));
        var req = new HttpRequestMessage();
        await auth.ApplyAsync(req);
        req.Headers.Authorization!.Parameter.Should().Be("dynamic-tok");
    }

    [Fact]
    public void BearerTokenAuthProvider_NullFactory_Throws()
    {
        FluentActions.Invoking(() => new BearerTokenAuthProvider((Func<CancellationToken, Task<string>>)null!))
            .Should().Throw<ArgumentNullException>();
    }
}
