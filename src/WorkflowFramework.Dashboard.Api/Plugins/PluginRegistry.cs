using WorkflowFramework.Dashboard.Api.Models;

namespace WorkflowFramework.Dashboard.Api.Plugins;

/// <summary>
/// Registry for step type plugins.
/// </summary>
public sealed class PluginRegistry
{
    private readonly List<IStepTypePlugin> _plugins = [];

    public IReadOnlyList<IStepTypePlugin> Plugins => _plugins;

    public void Register(IStepTypePlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        _plugins.Add(plugin);
    }

    public IStep? CreateStep(string type, string name, Dictionary<string, string>? config)
    {
        foreach (var plugin in _plugins)
        {
            var step = plugin.CreateStep(type, name, config);
            if (step is not null) return step;
        }
        return null;
    }

    public IReadOnlyList<StepTypeInfo> GetAllStepTypes()
    {
        return _plugins.SelectMany(p => p.StepTypes).ToList();
    }
}
