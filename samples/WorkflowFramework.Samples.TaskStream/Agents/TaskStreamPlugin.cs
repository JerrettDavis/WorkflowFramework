#pragma warning disable SKEXP0070
using System.ComponentModel;
using Microsoft.SemanticKernel;
using WorkflowFramework.Samples.TaskStream.Tools;

namespace WorkflowFramework.Samples.TaskStream.Agents;

/// <summary>
/// SK plugin that wraps the existing <see cref="IAgentTool"/> implementations,
/// exposing them as <see cref="KernelFunction"/>s for Semantic Kernel.
/// </summary>
public sealed class TaskStreamPlugin
{
    private readonly IReadOnlyDictionary<string, IAgentTool> _tools;

    public TaskStreamPlugin(IEnumerable<IAgentTool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name);
    }

    [KernelFunction("web_search")]
    [Description("Search the web for information")]
    public Task<string> WebSearchAsync(string query, CancellationToken ct = default) =>
        ExecuteToolAsync("web_search", query, ct);

    [KernelFunction("calendar")]
    [Description("Check or manage calendar events")]
    public Task<string> CalendarAsync(string input, CancellationToken ct = default) =>
        ExecuteToolAsync("calendar", input, ct);

    [KernelFunction("location")]
    [Description("Look up location information")]
    public Task<string> LocationAsync(string input, CancellationToken ct = default) =>
        ExecuteToolAsync("location", input, ct);

    [KernelFunction("deployment")]
    [Description("Manage deployments")]
    public Task<string> DeploymentAsync(string input, CancellationToken ct = default) =>
        ExecuteToolAsync("deployment", input, ct);

    [KernelFunction("file_system")]
    [Description("File system operations")]
    public Task<string> FileSystemAsync(string input, CancellationToken ct = default) =>
        ExecuteToolAsync("file_system", input, ct);

    private Task<string> ExecuteToolAsync(string toolName, string input, CancellationToken ct)
    {
        if (_tools.TryGetValue(toolName, out var tool))
            return tool.ExecuteAsync(input, ct);
        return Task.FromResult($"Tool '{toolName}' not found.");
    }
}
