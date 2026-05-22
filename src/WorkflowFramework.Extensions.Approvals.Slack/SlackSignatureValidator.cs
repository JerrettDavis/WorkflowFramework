using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace WorkflowFramework.Extensions.Approvals.Slack;

/// <summary>
/// Validates Slack request signatures using the <c>X-Slack-Signature</c> header
/// and the configured signing secret. Protects against replay attacks by enforcing
/// a configurable maximum request age.
/// </summary>
public sealed class SlackSignatureValidator
{
    private readonly SlackApprovalOptions _options;

    /// <summary>
    /// Initialises a new instance of <see cref="SlackSignatureValidator"/>.
    /// </summary>
    /// <param name="options">The Slack approval options containing the signing secret and max age.</param>
    public SlackSignatureValidator(IOptions<SlackApprovalOptions> options)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    }

    /// <summary>
    /// Validates a Slack request signature.
    /// </summary>
    /// <param name="timestamp">The value of the <c>X-Slack-Request-Timestamp</c> header (Unix seconds as string).</param>
    /// <param name="rawBody">The raw, unmodified request body.</param>
    /// <param name="signature">The value of the <c>X-Slack-Signature</c> header (format: <c>v0=hexdigest</c>).</param>
    /// <param name="now">
    /// Optional override for the current time, used for deterministic testing.
    /// When <see langword="null"/>, <see cref="DateTimeOffset.UtcNow"/> is used.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the signature is valid and the request is within the allowed age;
    /// <see langword="false"/> otherwise. Never throws.
    /// </returns>
    public bool Validate(string timestamp, string rawBody, string signature, DateTimeOffset? now = null)
    {
        try
        {
            if (string.IsNullOrEmpty(timestamp)) return false;
            if (rawBody is null) return false;
            if (string.IsNullOrEmpty(signature)) return false;

            if (!long.TryParse(timestamp, out var unixSeconds)) return false;

            var requestTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            var currentTime = now ?? DateTimeOffset.UtcNow;
            var age = Math.Abs((currentTime - requestTime).TotalSeconds);

            if (age > _options.RequestSignatureMaxAgeSeconds) return false;

            // Compute expected signature: v0=hex(HMAC-SHA256(signingSecret, "v0:{timestamp}:{rawBody}"))
            var signingBase = $"v0:{timestamp}:{rawBody}";
            var secretBytes = Encoding.UTF8.GetBytes(_options.SigningSecret);
            var messageBytes = Encoding.UTF8.GetBytes(signingBase);

            byte[] computedHash;
            using (var hmac = new HMACSHA256(secretBytes))
            {
                computedHash = hmac.ComputeHash(messageBytes);
            }

            var expectedSignature = "v0=" + Convert.ToHexString(computedHash).ToLowerInvariant();

            // Validate format: signature must start with "v0=" and have matching length
            if (!signature.StartsWith("v0=", StringComparison.Ordinal)) return false;
            if (signature.Length != expectedSignature.Length) return false;

            // Constant-time compare on the hex-decoded bytes to prevent timing attacks
            var expectedHex = expectedSignature[3..];
            var providedHex = signature[3..];

            if (expectedHex.Length != providedHex.Length) return false;

            byte[] expectedBytes;
            byte[] providedBytes;
            try
            {
                expectedBytes = Convert.FromHexString(expectedHex);
                providedBytes = Convert.FromHexString(providedHex);
            }
            catch (FormatException)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
        }
        catch
        {
            return false;
        }
    }
}
