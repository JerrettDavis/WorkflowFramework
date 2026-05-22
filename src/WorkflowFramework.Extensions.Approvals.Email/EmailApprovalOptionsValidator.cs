using Microsoft.Extensions.Options;

namespace WorkflowFramework.Extensions.Approvals.Email;

/// <summary>
/// Validates <see cref="EmailApprovalOptions"/> at host startup.
/// Registered automatically by <see cref="DependencyInjection.EmailApprovalsBuilderExtensions.UseEmail"/>.
/// </summary>
public sealed class EmailApprovalOptionsValidator : IValidateOptions<EmailApprovalOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, EmailApprovalOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.SmtpHost))
            failures.Add($"{nameof(options.SmtpHost)} must not be empty.");

        if (string.IsNullOrWhiteSpace(options.From))
            failures.Add($"{nameof(options.From)} must not be empty.");

        if (string.IsNullOrWhiteSpace(options.ApproveUrlTemplate))
        {
            failures.Add($"{nameof(options.ApproveUrlTemplate)} must not be empty.");
        }
        else if (!options.ApproveUrlTemplate.Contains("{token}", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"{nameof(options.ApproveUrlTemplate)} must contain the '{{token}}' placeholder.");
        }

        if (string.IsNullOrWhiteSpace(options.RejectUrlTemplate))
        {
            failures.Add($"{nameof(options.RejectUrlTemplate)} must not be empty.");
        }
        else if (!options.RejectUrlTemplate.Contains("{token}", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"{nameof(options.RejectUrlTemplate)} must contain the '{{token}}' placeholder.");
        }

        if (string.IsNullOrWhiteSpace(options.TokenSigningKey))
        {
            failures.Add($"{nameof(options.TokenSigningKey)} must not be empty.");
        }
        else
        {
            try
            {
                var keyBytes = Convert.FromBase64String(options.TokenSigningKey);
                if (keyBytes.Length < 32)
                    failures.Add($"{nameof(options.TokenSigningKey)} must decode to at least 32 bytes (got {keyBytes.Length}).");
            }
            catch (FormatException)
            {
                failures.Add($"{nameof(options.TokenSigningKey)} is not a valid base64 string.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
