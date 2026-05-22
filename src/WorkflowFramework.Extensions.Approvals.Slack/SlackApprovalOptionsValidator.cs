using Microsoft.Extensions.Options;

namespace WorkflowFramework.Extensions.Approvals.Slack;

/// <summary>
/// Validates <see cref="SlackApprovalOptions"/> at startup, ensuring all required fields
/// are present and properly formatted before the application begins handling requests.
/// </summary>
public sealed class SlackApprovalOptionsValidator : IValidateOptions<SlackApprovalOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SlackApprovalOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BotToken))
            errors.Add($"{nameof(SlackApprovalOptions.BotToken)} is required.");

        if (string.IsNullOrWhiteSpace(options.ChannelId))
            errors.Add($"{nameof(SlackApprovalOptions.ChannelId)} is required.");

        if (string.IsNullOrWhiteSpace(options.SigningSecret))
            errors.Add($"{nameof(SlackApprovalOptions.SigningSecret)} is required.");

        if (!string.IsNullOrWhiteSpace(options.ApiBaseUrl))
        {
            if (!Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out _))
                errors.Add($"{nameof(SlackApprovalOptions.ApiBaseUrl)} must be a valid absolute URI.");
        }
        else
        {
            errors.Add($"{nameof(SlackApprovalOptions.ApiBaseUrl)} is required.");
        }

        if (options.RequestSignatureMaxAgeSeconds <= 0)
            errors.Add($"{nameof(SlackApprovalOptions.RequestSignatureMaxAgeSeconds)} must be greater than 0.");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
