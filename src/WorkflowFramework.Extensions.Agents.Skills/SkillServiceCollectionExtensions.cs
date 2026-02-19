using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WorkflowFramework.Extensions.Agents.Skills;

/// <summary>
/// DI extension methods for agent skills.
/// </summary>
public static class SkillServiceCollectionExtensions
{
    /// <summary>
    /// Registers agent skills discovery, tool provider, and context source.
    /// </summary>
    public static IServiceCollection AddAgentSkills(
        this IServiceCollection services,
        Action<SkillOptions>? configure = null)
    {
        var options = new SkillOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);

        services.TryAddSingleton<SkillDiscovery>(sp =>
        {
            var opts = sp.GetRequiredService<SkillOptions>();
            return new SkillDiscovery(opts.ScanStandardPaths, opts.AdditionalPaths);
        });

        services.TryAddSingleton<IReadOnlyList<SkillDefinition>>(sp =>
        {
            var opts = sp.GetRequiredService<SkillOptions>();
            if (!opts.AutoDiscover) return Array.Empty<SkillDefinition>();
            var discovery = sp.GetRequiredService<SkillDiscovery>();
            return discovery.DiscoverAll();
        });

        services.TryAddSingleton<SkillToolProvider>(sp =>
            new SkillToolProvider(sp.GetRequiredService<IReadOnlyList<SkillDefinition>>()));
        services.AddSingleton<IToolProvider>(sp => sp.GetRequiredService<SkillToolProvider>());

        services.TryAddSingleton<SkillContextSource>(sp =>
            new SkillContextSource(sp.GetRequiredService<IReadOnlyList<SkillDefinition>>()));
        services.AddSingleton<IContextSource>(sp => sp.GetRequiredService<SkillContextSource>());

        return services;
    }
}
