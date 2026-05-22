using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WorkflowFramework.Extensions.Approvals;

namespace WorkflowFramework.Extensions.Approvals.Teams.DependencyInjection;

/// <summary>
/// Extension methods for registering the Microsoft Teams approval channel with
/// <see cref="IApprovalsBuilder"/>.
/// </summary>
public static class TeamsApprovalsBuilderExtensions
{
    /// <summary>
    /// Registers the Teams approval channel and all required supporting services.
    /// </summary>
    /// <param name="builder">The approvals builder.</param>
    /// <param name="configure">A delegate that configures <see cref="TeamsApprovalOptions"/>.</param>
    /// <returns>The same <see cref="IApprovalsBuilder"/> for chaining.</returns>
    /// <remarks>
    /// The following services are registered:
    /// <list type="bullet">
    ///   <item><see cref="TeamsApprovalOptions"/> via <see cref="OptionsServiceCollectionExtensions.Configure{TOptions}(IServiceCollection,System.Action{TOptions})"/>.</item>
    ///   <item><see cref="TeamsApprovalOptionsValidator"/> as <see cref="IValidateOptions{TOptions}"/>.</item>
    ///   <item><see cref="TeamsCallbackTokenService"/> as a singleton.</item>
    ///   <item><see cref="TeamsCallbackHandler"/> as a singleton.</item>
    ///   <item>A named <see cref="System.Net.Http.HttpClient"/> (<c>"approvals.teams"</c>).</item>
    ///   <item><see cref="TeamsApprovalChannel"/> registered via <see cref="IApprovalsBuilder.UseChannel{TChannel}"/>.</item>
    /// </list>
    /// </remarks>
    public static IApprovalsBuilder UseTeams(
        this IApprovalsBuilder builder,
        Action<TeamsApprovalOptions> configure)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var services = builder.Services;

        services.Configure(configure);
        services.AddSingleton<IValidateOptions<TeamsApprovalOptions>, TeamsApprovalOptionsValidator>();
        services.AddSingleton<TeamsCallbackTokenService>();
        services.AddSingleton<TeamsCallbackHandler>();
        services.AddHttpClient(new TeamsApprovalOptions().HttpClientName);

        // Register Lazy<PersistentApprovalService> to break the circular reference:
        // TeamsApprovalChannel -> Lazy<PersistentApprovalService>
        //   (deferred) -> PersistentApprovalService -> IApprovalChannel -> TeamsApprovalChannel
        services.TryAddSingleton(sp => new Lazy<PersistentApprovalService>(
            sp.GetRequiredService<PersistentApprovalService>));

        services.AddSingleton<TeamsApprovalChannel>();
        builder.UseChannel<TeamsApprovalChannel>();

        return builder;
    }
}
