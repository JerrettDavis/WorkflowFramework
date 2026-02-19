using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Batch;
using WorkflowFramework.Extensions.DataMapping.Engine;
using WorkflowFramework.Extensions.DataMapping.Readers;
using WorkflowFramework.Extensions.DataMapping.Transformers;
using WorkflowFramework.Extensions.DataMapping.Writers;

namespace WorkflowFramework.Extensions.DataMapping;

/// <summary>
/// DI registration extensions for the DataMapping engine.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the data mapping engine, built-in readers, writers, and transformers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDataMapping(this IServiceCollection services)
    {
        // Core engine
        services.AddSingleton<IFieldTransformerRegistry>(sp =>
        {
            var transformers = sp.GetServices<IFieldTransformer>();
            return new FieldTransformerRegistry(transformers);
        });
        services.AddSingleton<IDataMapper>(sp =>
        {
            var registry = sp.GetRequiredService<IFieldTransformerRegistry>();
            var readers = sp.GetServices<object>(); // Will be filtered by type in DataMapper
            var writers = sp.GetServices<object>();
            return new DataMapper(registry, readers, writers);
        });
        services.AddSingleton<IDataBatcher, DataBatcher>();

        // Built-in transformers
        services.AddSingleton<IFieldTransformer, ToUpperTransformer>();
        services.AddSingleton<IFieldTransformer, ToLowerTransformer>();
        services.AddSingleton<IFieldTransformer, TrimTransformer>();
        services.AddSingleton<IFieldTransformer, DateFormatTransformer>();
        services.AddSingleton<IFieldTransformer, NumberFormatTransformer>();
        services.AddSingleton<IFieldTransformer, BooleanTransformer>();
        services.AddSingleton<IFieldTransformer, RegexReplaceTransformer>();
        services.AddSingleton<IFieldTransformer, DefaultValueTransformer>();
        services.AddSingleton<IFieldTransformer, ConditionalTransformer>();

        return services;
    }
}
