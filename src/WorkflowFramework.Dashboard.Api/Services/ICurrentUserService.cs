using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace WorkflowFramework.Dashboard.Api.Services;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Username { get; }
    bool IsAuthenticated { get; }
}

public sealed class HttpCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public HttpCurrentUserService(IHttpContextAccessor accessor) => _httpContextAccessor = accessor;

    public string? UserId => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public string? Username => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value;
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}

/// <summary>
/// Used in anonymous mode — always returns the system user.
/// </summary>
public sealed class AnonymousCurrentUserService : ICurrentUserService
{
    public string? UserId => "system";
    public string? Username => "system";
    public bool IsAuthenticated => false;
}
