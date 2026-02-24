namespace WorkflowFramework.Dashboard.Web.Services;

public sealed class AuthStateService
{
    public bool IsAuthenticated { get; private set; }
    public string? UserId { get; private set; }
    public string? Username { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Token { get; private set; }
    public string? RefreshToken { get; private set; }

    public event Action? OnAuthStateChanged;

    public void SetAuthenticated(string userId, string username, string? displayName, string token, string? refreshToken = null)
    {
        IsAuthenticated = true;
        UserId = userId;
        Username = username;
        DisplayName = displayName;
        Token = token;
        RefreshToken = refreshToken;
        OnAuthStateChanged?.Invoke();
    }

    public void Logout()
    {
        IsAuthenticated = false;
        UserId = Username = DisplayName = Token = RefreshToken = null;
        OnAuthStateChanged?.Invoke();
    }
}
