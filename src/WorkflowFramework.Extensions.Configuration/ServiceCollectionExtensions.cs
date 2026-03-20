using Microsoft.Extensions.DependencyInjection;

namespace WorkflowFramework.Extensions.Configuration;

/// <summary>
/// Extension methods for registering workflow definition loader services.
/// </summary>
public static class WorkflowDefinitionLoaderServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="YamlWorkflowDefinitionLoader"/> as the <see cref="IWorkflowDefinitionLoader"/>
    /// implementation in the dependency-injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddYamlWorkflowLoader(this IServiceCollection services)
    {
        services.AddSingleton<IWorkflowDefinitionLoader, YamlWorkflowDefinitionLoader>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="JsonWorkflowDefinitionLoader"/> as the <see cref="IWorkflowDefinitionLoader"/>
    /// implementation in the dependency-injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJsonWorkflowLoader(this IServiceCollection services)
    {
        services.AddSingleton<IWorkflowDefinitionLoader, JsonWorkflowDefinitionLoader>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="StepRegistry"/> as both <see cref="IStepRegistry"/> and the concrete
    /// <see cref="StepRegistry"/> type in the dependency-injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStepRegistry(this IServiceCollection services)
    {
        services.AddSingleton<StepRegistry>();
        services.AddSingleton<IStepRegistry>(sp => sp.GetRequiredService<StepRegistry>());
        return services;
    }

    /// <summary>
    /// Registers <see cref="WorkflowDefinitionBuilder"/> in the dependency-injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWorkflowDefinitionBuilder(this IServiceCollection services)
    {
        services.AddTransient<WorkflowDefinitionBuilder>();
        return services;
    }
}
