using WorkflowFramework.Serialization;

namespace WorkflowFramework.Cli.Commands;

internal static class WorkflowFileHelper
{
    public static WorkflowDefinitionDto Deserialize(string filePath, string content)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".json" => WorkflowSerializer.FromJson(content),
            ".yaml" or ".yml" => WorkflowSerializer.FromYaml(content),
            _ => throw new InvalidOperationException($"Unsupported file extension: {ext}. Use .json, .yaml, or .yml.")
        };
    }
}
