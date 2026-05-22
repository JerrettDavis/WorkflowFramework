using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace WorkflowFramework.Extensions.Approvals.Teams;

/// <summary>
/// Creates and verifies short-lived HMAC-SHA256 tokens embedded in Adaptive Card
/// action data to authenticate Teams callback payloads.
/// </summary>
/// <remarks>
/// Token format (before Base64Url encoding of each part):
/// <code>
/// {payload}.{signature}
/// </code>
/// where <c>payload</c> is <c>Base64Url({correlationId}|{0|1}|{unixSecondsExp})</c>
/// and <c>signature</c> is <c>Base64Url(HMAC-SHA256(key, payload))</c>.
/// </remarks>
public sealed class TeamsCallbackTokenService
{
    private readonly byte[] _key;

    /// <summary>
    /// Initialises a new <see cref="TeamsCallbackTokenService"/> using the shared secret
    /// from <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The Teams approval options containing the shared secret.</param>
    public TeamsCallbackTokenService(IOptions<TeamsApprovalOptions> options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        _key = Encoding.UTF8.GetBytes(options.Value.CallbackSharedSecret);
    }

    /// <summary>
    /// Creates a signed token encoding the given parameters.
    /// </summary>
    /// <param name="correlationId">The correlation ID of the pending approval.</param>
    /// <param name="decision"><see langword="true"/> for approve; <see langword="false"/> for reject.</param>
    /// <param name="expiresAt">The absolute expiry of the token.</param>
    /// <returns>A URL-safe Base64 token string.</returns>
    public string Create(string correlationId, bool decision, DateTimeOffset expiresAt)
    {
        if (correlationId is null) throw new ArgumentNullException(nameof(correlationId));

        var payloadText = BuildPayloadText(correlationId, decision, expiresAt);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadText);
        var payloadB64 = Base64UrlEncode(payloadBytes);

        var sig = ComputeSignature(payloadB64);
        return $"{payloadB64}.{sig}";
    }

    /// <summary>
    /// Attempts to verify and decode a token previously produced by <see cref="Create"/>.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    /// <param name="token">The token to verify.</param>
    /// <param name="correlationId">On success, the correlation ID encoded in the token.</param>
    /// <param name="decision">On success, the decision encoded in the token.</param>
    /// <param name="expiresAt">On success, the expiry encoded in the token.</param>
    /// <returns>
    /// <see langword="true"/> if the token is structurally valid, signature matches, and
    /// has not expired; <see langword="false"/> otherwise.
    /// </returns>
    public bool TryVerify(
        string token,
        out string correlationId,
        out bool decision,
        out DateTimeOffset expiresAt)
    {
        correlationId = string.Empty;
        decision = false;
        expiresAt = default;

        if (string.IsNullOrEmpty(token))
            return false;

        try
        {
            var dotIndex = token.LastIndexOf('.');
            if (dotIndex <= 0 || dotIndex == token.Length - 1)
                return false;

            var payloadB64 = token[..dotIndex];
            var providedSig = token[(dotIndex + 1)..];

            // Constant-time signature comparison.
            var expectedSig = ComputeSignature(payloadB64);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedSig);
            var providedBytes = Encoding.UTF8.GetBytes(providedSig);

            if (!CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
                return false;

            var payloadText = Encoding.UTF8.GetString(Base64UrlDecode(payloadB64));
            var parts = payloadText.Split('|');
            if (parts.Length != 3)
                return false;

            correlationId = parts[0];
            decision = parts[1] == "1";
            var unixSeconds = long.Parse(parts[2]);
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

            if (DateTimeOffset.UtcNow >= expiresAt)
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private string ComputeSignature(string payloadB64)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payloadB64);
        var hash = HMACSHA256.HashData(_key, payloadBytes);
        return Base64UrlEncode(hash);
    }

    private static string BuildPayloadText(string correlationId, bool decision, DateTimeOffset expiresAt) =>
        $"{correlationId}|{(decision ? "1" : "0")}|{expiresAt.ToUnixTimeSeconds()}";

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string base64Url)
    {
        var padded = base64Url
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
