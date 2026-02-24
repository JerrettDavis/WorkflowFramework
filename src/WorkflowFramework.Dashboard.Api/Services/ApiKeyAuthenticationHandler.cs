using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WorkflowFramework.Dashboard.Api.Services;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IAuthService _authService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAuthService authService)
        : base(options, logger, encoder)
    {
        _authService = authService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
            return AuthenticateResult.NoResult();

        var apiKey = apiKeyHeader.ToString();
        if (string.IsNullOrEmpty(apiKey))
            return AuthenticateResult.NoResult();

        var result = await _authService.ValidateApiKeyAsync(apiKey);
        if (result is null)
            return AuthenticateResult.Fail("Invalid API key.");

        var (userId, username) = result.Value;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId ?? ""),
            new Claim(ClaimTypes.Name, username ?? ""),
            new Claim("auth_method", "api_key")
        };

        var identity = new ClaimsIdentity(claims, "ApiKey");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "ApiKey");
        return AuthenticateResult.Success(ticket);
    }
}
