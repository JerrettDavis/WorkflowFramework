using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Persistence;
using WorkflowFramework.Dashboard.Persistence.Entities;

namespace WorkflowFramework.Dashboard.Api.Services;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<DashboardUser?> GetUserAsync(string userId, CancellationToken ct = default);
    Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<CreateApiKeyResponse> CreateApiKeyAsync(string userId, CreateApiKeyRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKeyResponse>> ListApiKeysAsync(string userId, CancellationToken ct = default);
    Task<bool> RevokeApiKeyAsync(string userId, string keyId, CancellationToken ct = default);
    Task<(string? UserId, string? Username)?> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default);
}

public sealed class AuthService : IAuthService
{
    private readonly DashboardDbContext _db;
    private readonly IConfiguration _configuration;

    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA256;

    public AuthService(DashboardDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
            return new AuthResult { Error = "Username must be at least 3 characters." };
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return new AuthResult { Error = "Password must be at least 8 characters." };

        if (await _db.Users.AnyAsync(u => u.Username == request.Username, ct))
            return new AuthResult { Error = "Username already taken." };

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(request.Password), salt, Iterations, HashAlgorithm, HashSize);

        var user = new DashboardUser
        {
            Username = request.Username,
            Email = request.Email,
            DisplayName = request.DisplayName ?? request.Username,
            PasswordHash = Convert.ToBase64String(hash),
            PasswordSalt = Convert.ToBase64String(salt),
            CreatedAt = DateTimeOffset.UtcNow,
            LastLoginAt = DateTimeOffset.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return GenerateTokenResult(user);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username, ct);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            return new AuthResult { Error = "Invalid username or password." };

        if (!VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
            return new AuthResult { Error = "Invalid username or password." };

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return GenerateTokenResult(user);
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = HashRefreshToken(refreshToken);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == tokenHash, ct);

        if (user is null || user.RefreshTokenExpiry < DateTimeOffset.UtcNow)
            return new AuthResult { Error = "Invalid or expired refresh token." };

        return GenerateTokenResult(user);
    }

    public async Task<DashboardUser?> GetUserAsync(string userId, CancellationToken ct = default) =>
        await _db.Users.FindAsync([userId], ct);

    public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8) return false;

        var user = await _db.Users.FindAsync([userId], ct);
        if (user is null) return false;

        if (!VerifyPassword(currentPassword, user.PasswordHash, user.PasswordSalt))
            return false;

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(newPassword), salt, Iterations, HashAlgorithm, HashSize);
        user.PasswordHash = Convert.ToBase64String(hash);
        user.PasswordSalt = Convert.ToBase64String(salt);
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CreateApiKeyResponse> CreateApiKeyAsync(string userId, CreateApiKeyRequest request, CancellationToken ct = default)
    {
        var plainKey = $"wfk_{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32))}";
        var keyHash = HashApiKey(plainKey);

        var entity = new ApiKeyEntity
        {
            UserId = userId,
            Name = request.Name,
            KeyHash = keyHash,
            KeyPrefix = plainKey[..12],
            ScopesJson = JsonSerializer.Serialize(request.Scopes),
            ExpiresAt = request.ExpiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.ApiKeys.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new CreateApiKeyResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            KeyPrefix = entity.KeyPrefix,
            Scopes = request.Scopes,
            CreatedAt = entity.CreatedAt,
            ExpiresAt = entity.ExpiresAt,
            Key = plainKey
        };
    }

    public async Task<IReadOnlyList<ApiKeyResponse>> ListApiKeysAsync(string userId, CancellationToken ct = default)
    {
        var keys = await _db.ApiKeys.Where(k => k.UserId == userId).ToListAsync(ct);
        return keys.Select(k => new ApiKeyResponse
        {
            Id = k.Id,
            Name = k.Name,
            KeyPrefix = k.KeyPrefix,
            Scopes = JsonSerializer.Deserialize<List<string>>(k.ScopesJson) ?? [],
            CreatedAt = k.CreatedAt,
            ExpiresAt = k.ExpiresAt,
            LastUsedAt = k.LastUsedAt,
            IsRevoked = k.IsRevoked
        }).ToList();
    }

    public async Task<bool> RevokeApiKeyAsync(string userId, string keyId, CancellationToken ct = default)
    {
        var key = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, ct);
        if (key is null) return false;
        key.IsRevoked = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(string? UserId, string? Username)?> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        var keyHash = HashApiKey(apiKey);
        var entity = await _db.ApiKeys.Include(k => k.User).FirstOrDefaultAsync(k => k.KeyHash == keyHash, ct);
        if (entity is null || entity.IsRevoked) return null;
        if (entity.ExpiresAt.HasValue && entity.ExpiresAt < DateTimeOffset.UtcNow) return null;

        entity.LastUsedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return (entity.UserId, entity.User?.Username);
    }

    private AuthResult GenerateTokenResult(DashboardUser user)
    {
        var jwtSecret = GetJwtSecret();
        var expiry = TimeSpan.FromHours(_configuration.GetValue("Dashboard:JwtExpiryHours", 24));
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "WorkflowFramework.Dashboard",
            audience: "WorkflowFramework.Dashboard",
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        user.RefreshToken = HashRefreshToken(refreshToken);
        user.RefreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(30);

        // SaveChanges will be called by the caller or already done
        _db.SaveChanges();

        return new AuthResult
        {
            Success = true,
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken = refreshToken,
            UserId = user.Id,
            Username = user.Username,
            ExpiresAt = expiresAt
        };
    }

    internal string GetJwtSecret()
    {
        var secret = _configuration["Dashboard:JwtSecret"];
        if (string.IsNullOrEmpty(secret))
        {
            // Generate a stable default for development
            secret = "WorkflowFramework_Development_JWT_Secret_Key_That_Is_At_Least_64_Bytes_Long!";
        }
        return secret;
    }

    private static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        var salt = Convert.FromBase64String(storedSalt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithm, HashSize);
        return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(storedHash));
    }

    private static string HashRefreshToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string HashApiKey(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
