using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace WorkflowFramework.Extensions.Approvals.Email;

/// <summary>
/// Creates and validates HMAC-SHA256-signed approval callback tokens.
/// Tokens are URL-safe base64url strings in the format
/// <c>{base64url-json}.{base64url-hmac}</c>.
/// </summary>
public sealed class ApprovalTokenService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly byte[] _signingKey;

    /// <summary>
    /// Initialises a new instance of <see cref="ApprovalTokenService"/>.
    /// </summary>
    /// <param name="options">The email approval options providing the signing key. Must not be <see langword="null"/>.</param>
    public ApprovalTokenService(IOptions<EmailApprovalOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _signingKey = Convert.FromBase64String(options.Value.TokenSigningKey);
    }

    /// <summary>
    /// Creates a signed token encoding the given <paramref name="payload"/>.
    /// </summary>
    /// <param name="payload">The payload to encode. Must not be <see langword="null"/>.</param>
    /// <returns>A URL-safe base64url token string.</returns>
    public string Create(ApprovalTokenPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var encodedJson = Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        var hmac = ComputeHmac(encodedJson);
        var encodedHmac = Base64UrlEncode(hmac);

        return $"{encodedJson}.{encodedHmac}";
    }

    /// <summary>
    /// Attempts to parse and validate a token produced by <see cref="Create"/>.
    /// Returns <see langword="false"/> when the token is malformed, the HMAC does not verify,
    /// or the token has expired. Never throws.
    /// </summary>
    /// <param name="token">The token string to validate.</param>
    /// <param name="payload">When this method returns <see langword="true"/>, contains the decoded payload.</param>
    /// <returns>
    /// <see langword="true"/> when the token is valid and not expired;
    /// <see langword="false"/> otherwise.
    /// </returns>
    public bool TryParse(string? token, out ApprovalTokenPayload payload)
    {
        payload = null!;

        if (string.IsNullOrEmpty(token))
            return false;

        try
        {
            var dotIndex = token.IndexOf('.', StringComparison.Ordinal);
            if (dotIndex < 0)
                return false;

            var encodedJson = token[..dotIndex];
            var encodedHmac = token[(dotIndex + 1)..];

            if (string.IsNullOrEmpty(encodedJson) || string.IsNullOrEmpty(encodedHmac))
                return false;

            // Verify HMAC with constant-time comparison.
            var expectedHmac = ComputeHmac(encodedJson);
            var actualHmac = Base64UrlDecode(encodedHmac);

            if (!CryptographicOperations.FixedTimeEquals(expectedHmac, actualHmac))
                return false;

            var jsonBytes = Base64UrlDecode(encodedJson);
            var decoded = JsonSerializer.Deserialize<ApprovalTokenPayload>(jsonBytes, SerializerOptions);

            if (decoded is null)
                return false;

            if (DateTimeOffset.UtcNow > decoded.ExpiresAt)
                return false;

            payload = decoded;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private byte[] ComputeHmac(string data)
    {
        using var hmac = new HMACSHA256(_signingKey);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    internal static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    internal static byte[] Base64UrlDecode(string value)
    {
        var padded = value
            .Replace('-', '+')
            .Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
