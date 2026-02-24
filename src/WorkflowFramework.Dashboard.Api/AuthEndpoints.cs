using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;

namespace WorkflowFramework.Dashboard.Api;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.RegisterAsync(request, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).WithName("Register").AllowAnonymous();

        group.MapPost("/login", async (LoginRequest request, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.LoginAsync(request, ct);
            return result.Success ? Results.Ok(result) : Results.Unauthorized();
        }).WithName("Login").AllowAnonymous();

        group.MapPost("/refresh", async (RefreshRequest request, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.RefreshTokenAsync(request.RefreshToken, ct);
            return result.Success ? Results.Ok(result) : Results.Unauthorized();
        }).WithName("RefreshToken").AllowAnonymous();

        group.MapPost("/change-password", async (ChangePasswordRequest request, IAuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();
            var ok = await auth.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);
            return ok ? Results.Ok(new { success = true }) : Results.BadRequest(new { error = "Invalid current password or new password too short." });
        }).WithName("ChangePassword").RequireAuthorization();

        group.MapGet("/me", async (IAuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();
            var user = await auth.GetUserAsync(userId, ct);
            if (user is null) return Results.NotFound();
            return Results.Ok(new
            {
                user.Id,
                user.Username,
                user.DisplayName,
                user.Email,
                user.CreatedAt,
                user.LastLoginAt
            });
        }).WithName("GetCurrentUser").RequireAuthorization();

        // API Key endpoints
        group.MapPost("/api-keys", async (CreateApiKeyRequest request, IAuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();
            var result = await auth.CreateApiKeyAsync(userId, request, ct);
            return Results.Ok(result);
        }).WithName("CreateApiKey").RequireAuthorization();

        group.MapGet("/api-keys", async (IAuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();
            var keys = await auth.ListApiKeysAsync(userId, ct);
            return Results.Ok(keys);
        }).WithName("ListApiKeys").RequireAuthorization();

        group.MapDelete("/api-keys/{id}", async (string id, IAuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();
            var ok = await auth.RevokeApiKeyAsync(userId, id, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        }).WithName("RevokeApiKey").RequireAuthorization();

        return endpoints;
    }
}

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = "";
}
