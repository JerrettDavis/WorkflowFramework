namespace WorkflowFramework.Extensions.Http;

/// <summary>
/// A workflow step that makes an HTTP request.
/// </summary>
public sealed class HttpStep : IStep
{
    private readonly HttpStepOptions _options;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="HttpStep"/>.
    /// </summary>
    /// <param name="options">The HTTP step options.</param>
    /// <param name="httpClient">The HTTP client to use.</param>
    public HttpStep(HttpStepOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <inheritdoc />
    public string Name => _options.Name ?? $"Http{_options.Method}";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        using var request = new HttpRequestMessage(_options.Method, _options.Url);

        foreach (var header in _options.Headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (_options.Body != null)
            request.Content = new StringContent(_options.Body, System.Text.Encoding.UTF8, _options.ContentType ?? "application/json");

        var response = await _httpClient.SendAsync(request, context.CancellationToken).ConfigureAwait(false);

        context.Properties[$"{Name}.StatusCode"] = (int)response.StatusCode;
        context.Properties[$"{Name}.Body"] = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        context.Properties[$"{Name}.IsSuccess"] = response.IsSuccessStatusCode;

        if (_options.EnsureSuccessStatusCode)
            response.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// Options for configuring an HTTP step.
/// </summary>
public sealed class HttpStepOptions
{
    /// <summary>Gets or sets the step name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the HTTP method.</summary>
    public HttpMethod Method { get; set; } = HttpMethod.Get;

    /// <summary>Gets or sets the request headers.</summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>Gets or sets the request body.</summary>
    public string? Body { get; set; }

    /// <summary>Gets or sets the content type.</summary>
    public string? ContentType { get; set; } = "application/json";

    /// <summary>Gets or sets whether to throw on non-success status codes.</summary>
    public bool EnsureSuccessStatusCode { get; set; } = true;
}
