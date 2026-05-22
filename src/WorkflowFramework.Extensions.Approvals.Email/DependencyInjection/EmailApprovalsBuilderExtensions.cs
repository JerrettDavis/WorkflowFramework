using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace WorkflowFramework.Extensions.Approvals.Email.DependencyInjection;

/// <summary>
/// Extension methods for wiring the email approval channel into the approvals builder.
/// </summary>
public static class EmailApprovalsBuilderExtensions
{
    /// <summary>
    /// Adds the email approval channel to the workflow approvals pipeline.
    /// </summary>
    /// <param name="builder">The approvals builder to configure.</param>
    /// <param name="configure">A delegate that configures the <see cref="EmailApprovalOptions"/>.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddApprovals()
    ///     .UseEmail(opts =>
    ///     {
    ///         opts.SmtpHost = "smtp.example.com";
    ///         opts.From = "approvals@example.com";
    ///         opts.ApproveUrlTemplate = "https://example.com/approvals/email/respond?t={token}";
    ///         opts.RejectUrlTemplate  = "https://example.com/approvals/email/respond?t={token}";
    ///         opts.TokenSigningKey = Environment.GetEnvironmentVariable("APPROVAL_SIGNING_KEY")!;
    ///     });
    /// </code>
    /// </example>
    public static IApprovalsBuilder UseEmail(
        this IApprovalsBuilder builder,
        Action<EmailApprovalOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services
            .AddOptions<EmailApprovalOptions>()
            .Configure(configure)
            .ValidateOnStart();

        builder.Services.TryAddSingleton<IValidateOptions<EmailApprovalOptions>, EmailApprovalOptionsValidator>();
        builder.Services.TryAddSingleton<ApprovalTokenService>();
        builder.Services.TryAddSingleton<IEmailSender, SmtpEmailSender>();

        // Register Lazy<PersistentApprovalService> to break the circular reference:
        // EmailApprovalChannel -> Lazy<PersistentApprovalService>
        //   (deferred) -> PersistentApprovalService -> IApprovalChannel -> EmailApprovalChannel
        builder.Services.TryAddSingleton(sp => new Lazy<PersistentApprovalService>(
            sp.GetRequiredService<PersistentApprovalService>));

        builder.Services.AddSingleton<EmailApprovalChannel>();

        builder.UseChannel<EmailApprovalChannel>();

        return builder;
    }
}
