using Microsoft.Extensions.Options;

namespace WorkflowFramework.Extensions.Approvals.Teams;

/// <summary>
/// Validates <see cref="TeamsApprovalOptions"/> at application startup.
/// </summary>
public sealed class TeamsApprovalOptionsValidator : IValidateOptions<TeamsApprovalOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, TeamsApprovalOptions options)
    {
        if (options is null)
            return ValidateOptionsResult.Fail("Options must not be null.");

        var errors = new List<string>();

        switch (options.Mode)
        {
            case TeamsApprovalMode.IncomingWebhook:
                if (string.IsNullOrWhiteSpace(options.WebhookUrl))
                {
                    errors.Add($"{nameof(options.WebhookUrl)} is required when Mode is IncomingWebhook.");
                }
                else if (!Uri.TryCreate(options.WebhookUrl, UriKind.Absolute, out var webhookUri) ||
                         !string.Equals(webhookUri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{nameof(options.WebhookUrl)} must be a valid HTTPS URI.");
                }
                break;

            case TeamsApprovalMode.Bot:
                if (string.IsNullOrWhiteSpace(options.BotServiceUrl))
                    errors.Add($"{nameof(options.BotServiceUrl)} is required when Mode is Bot.");

                if (string.IsNullOrWhiteSpace(options.BotConversationId))
                    errors.Add($"{nameof(options.BotConversationId)} is required when Mode is Bot.");

                if (string.IsNullOrWhiteSpace(options.BotAppId))
                    errors.Add($"{nameof(options.BotAppId)} is required when Mode is Bot.");

                if (string.IsNullOrWhiteSpace(options.BotAppPassword))
                    errors.Add($"{nameof(options.BotAppPassword)} is required when Mode is Bot.");
                break;

            default:
                errors.Add($"Unknown Mode value '{options.Mode}'.");
                break;
        }

        if (string.IsNullOrWhiteSpace(options.CallbackSharedSecret) ||
            options.CallbackSharedSecret.Length < 16)
        {
            errors.Add($"{nameof(options.CallbackSharedSecret)} is required and must be at least 16 characters.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
