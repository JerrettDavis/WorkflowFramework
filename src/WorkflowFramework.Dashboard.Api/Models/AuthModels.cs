namespace WorkflowFramework.Dashboard.Api.Models;

public sealed class RegisterRequest
{
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? DisplayName { get; set; }
}

public sealed class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class AuthResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public sealed class CreateApiKeyRequest
{
    public string Name { get; set; } = "";
    public List<string> Scopes { get; set; } = [];
    public DateTimeOffset? ExpiresAt { get; set; }
}

public class ApiKeyResponse
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string KeyPrefix { get; set; } = "";
    public List<string> Scopes { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public bool IsRevoked { get; set; }
}

public sealed class CreateApiKeyResponse : ApiKeyResponse
{
    /// <summary>The full API key — only returned once at creation time.</summary>
    public string Key { get; set; } = "";
}
