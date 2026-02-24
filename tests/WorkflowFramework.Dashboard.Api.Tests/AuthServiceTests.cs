using Xunit;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Dashboard.Persistence;

namespace WorkflowFramework.Dashboard.Api.Tests;

public sealed class AuthServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DashboardDbContext _db;
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<DashboardDbContext>()
            .UseSqlite(_connection).Options;
        _db = new DashboardDbContext(options);
        _db.Database.EnsureCreated();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dashboard:JwtSecret"] = "TestSecret_That_Is_At_Least_64_Bytes_Long_For_HMAC_SHA256_Signing!"
            })
            .Build();

        _auth = new AuthService(_db, config);
    }

    [Fact]
    public async Task Register_CreatesUser_ReturnsTokens()
    {
        var result = await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "password123",
            DisplayName = "Test User"
        });

        result.Success.Should().BeTrue();
        result.Token.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.UserId.Should().NotBeNullOrEmpty();
        result.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task Register_RejectsShortUsername()
    {
        var result = await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "ab", Password = "password123"
        });
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Username");
    }

    [Fact]
    public async Task Register_RejectsShortPassword()
    {
        var result = await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "testuser", Password = "short"
        });
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Password");
    }

    [Fact]
    public async Task Register_RejectsDuplicateUsername()
    {
        await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "testuser", Password = "password123"
        });
        var result = await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "testuser", Password = "password456"
        });
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("taken");
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "loginuser", Password = "password123"
        });

        var result = await _auth.LoginAsync(new LoginRequest
        {
            Username = "loginuser", Password = "password123"
        });

        result.Success.Should().BeTrue();
        result.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Fails()
    {
        await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "loginuser", Password = "password123"
        });

        var result = await _auth.LoginAsync(new LoginRequest
        {
            Username = "loginuser", Password = "wrongpassword"
        });

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Login_WithNonexistentUser_Fails()
    {
        var result = await _auth.LoginAsync(new LoginRequest
        {
            Username = "nobody", Password = "password123"
        });
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
    {
        var reg = await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "refreshuser", Password = "password123"
        });

        var result = await _auth.RefreshTokenAsync(reg.RefreshToken!);
        result.Success.Should().BeTrue();
        result.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_Fails()
    {
        var result = await _auth.RefreshTokenAsync("invalid-token");
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ChangePassword_WithCorrectCurrent_Succeeds()
    {
        var reg = await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "changepw", Password = "oldpassword1"
        });

        var ok = await _auth.ChangePasswordAsync(reg.UserId!, "oldpassword1", "newpassword1");
        ok.Should().BeTrue();

        // Can login with new password
        var login = await _auth.LoginAsync(new LoginRequest
        {
            Username = "changepw", Password = "newpassword1"
        });
        login.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrent_Fails()
    {
        var reg = await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "changepw2", Password = "oldpassword1"
        });

        var ok = await _auth.ChangePasswordAsync(reg.UserId!, "wrongpassword", "newpassword1");
        ok.Should().BeFalse();
    }

    [Fact]
    public async Task GetUser_ReturnsUser()
    {
        var reg = await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "getuser", Password = "password123", DisplayName = "Get User"
        });

        var user = await _auth.GetUserAsync(reg.UserId!);
        user.Should().NotBeNull();
        user!.Username.Should().Be("getuser");
        user.DisplayName.Should().Be("Get User");
    }

    [Fact]
    public async Task ApiKey_CreateListRevoke()
    {
        var reg = await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "apiuser", Password = "password123"
        });

        var created = await _auth.CreateApiKeyAsync(reg.UserId!, new CreateApiKeyRequest
        {
            Name = "Test Key",
            Scopes = ["workflows:read"]
        });

        created.Key.Should().StartWith("wfk_");
        created.Name.Should().Be("Test Key");

        var keys = await _auth.ListApiKeysAsync(reg.UserId!);
        keys.Should().HaveCount(1);
        keys[0].Name.Should().Be("Test Key");

        // Validate the key
        var validated = await _auth.ValidateApiKeyAsync(created.Key);
        validated.Should().NotBeNull();
        validated!.Value.UserId.Should().Be(reg.UserId);

        // Revoke
        var revoked = await _auth.RevokeApiKeyAsync(reg.UserId!, created.Id);
        revoked.Should().BeTrue();

        // Validate fails after revoke
        var validatedAfter = await _auth.ValidateApiKeyAsync(created.Key);
        validatedAfter.Should().BeNull();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
