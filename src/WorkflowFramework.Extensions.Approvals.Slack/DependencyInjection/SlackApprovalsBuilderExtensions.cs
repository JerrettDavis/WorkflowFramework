using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace WorkflowFramework.Extensions.Approvals.Slack.DependencyInjection;

/// <summary>
/// Provides extension methods on <see cref="IApprovalsBuilder"/> for registering the Slack
/// approval channel and its dependencies.
/// </summary>
public static class SlackApprovalsBuilderExtensions
{
    /// <summary>
    /// Registers the Slack approval channel, signature validator, interactivity handler,
    /// and named HTTP client.
    /// </summary>
    /// <param name="builder">The approvals builder to configure.</param>
    /// <param name="configure">A delegate to configure <see cref="SlackApprovalOptions"/>.</param>
    /// <returns>The same <see cref="IApprovalsBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public static IApprovalsBuilder UseSlack(
        this IApprovalsBuilder builder,
        Action<SlackApprovalOptions> configure)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        builder.Services
            .AddOptions<SlackApprovalOptions>()
            .Configure(configure)
            .ValidateOnStart();

        builder.Services.TryAddSingleton<IValidateOptions<SlackApprovalOptions>, SlackApprovalOptionsValidator>();
        builder.Services.TryAddSingleton<SlackSignatureValidator>();
        builder.Services.TryAddSingleton<SlackInteractivityHandler>();
        builder.Services.AddHttpClient("approvals.slack");

        // Register Lazy<PersistentApprovalService> to break the circular reference:
        // SlackApprovalChannel -> Lazy<PersistentApprovalService>
        //   (deferred) -> PersistentApprovalService -> IApprovalChannel -> SlackApprovalChannel
        builder.Services.TryAddSingleton(sp => new Lazy<PersistentApprovalService>(
            sp.GetRequiredService<PersistentApprovalService>));

        builder.Services.AddSingleton<SlackApprovalChannel>();

        builder.UseChannel<SlackApprovalChannel>();

        return builder;
    }
}
