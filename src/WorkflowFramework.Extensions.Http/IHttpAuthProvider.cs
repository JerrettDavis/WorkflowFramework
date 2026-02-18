namespace WorkflowFramework.Extensions.Http;

/// <summary>
/// Abstraction for HTTP authentication providers.
/// </summary>
public interface IHttpAuthProvider
{
    /// <summary>Applies authentication to the given request.</summary>
    /// <param name="request">The HTTP request to authenticate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}

/// <summary>
/// API key authentication provider.
/// </summary>
public sealed class ApiKeyAuthProvider : IHttpAuthProvider
{
    private readonly string _headerName;
    private readonly string _apiKey;

    /// <summary>
    /// Initializes a new instance of <see cref="ApiKeyAuthProvider"/>.
    /// </summary>
    /// <param name="apiKey">The API key.</param>
    /// <param name="headerName">The header name (default: X-Api-Key).</param>
    public ApiKeyAuthProvider(string apiKey, string headerName = "X-Api-Key")
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _headerName = headerName;
    }

    /// <inheritdoc />
    public Task ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        request.Headers.TryAddWithoutValidation(_headerName, _apiKey);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Bearer token authentication provider.
/// </summary>
public sealed class BearerTokenAuthProvider : IHttpAuthProvider
{
    private readonly Func<CancellationToken, Task<string>> _tokenFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="BearerTokenAuthProvider"/>.
    /// </summary>
    /// <param name="token">The static bearer token.</param>
    public BearerTokenAuthProvider(string token)
    {
        _tokenFactory = _ => Task.FromResult(token);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="BearerTokenAuthProvider"/> with a dynamic token factory.
    /// </summary>
    /// <param name="tokenFactory">Factory to produce the token.</param>
    public BearerTokenAuthProvider(Func<CancellationToken, Task<string>> tokenFactory)
    {
        _tokenFactory = tokenFactory ?? throw new ArgumentNullException(nameof(tokenFactory));
    }

    /// <inheritdoc />
    public async Task ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var token = await _tokenFactory(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
}
