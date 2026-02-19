using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkflowFramework.Serialization;

/// <summary>
/// Serializes and deserializes workflow definitions to/from JSON and YAML.
/// </summary>
public static class WorkflowSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── JSON ──

    /// <summary>Serializes an IWorkflow to JSON.</summary>
    public static string ToJson(IWorkflow workflow)
    {
        var dto = ToDefinition(workflow);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>Deserializes a WorkflowDefinitionDto from JSON.</summary>
    public static WorkflowDefinitionDto FromJson(string json)
    {
        return JsonSerializer.Deserialize<WorkflowDefinitionDto>(json, JsonOptions)
            ?? throw new JsonException("Failed to deserialize workflow definition.");
    }

    // ── YAML ──

    /// <summary>Serializes an IWorkflow to YAML.</summary>
    public static string ToYaml(IWorkflow workflow)
    {
        var dto = ToDefinition(workflow);
        return YamlWriter.Write(dto);
    }

    /// <summary>Deserializes a WorkflowDefinitionDto from YAML.</summary>
    public static WorkflowDefinitionDto FromYaml(string yaml)
    {
        return YamlReader.Read(yaml);
    }

    // ── Shared ──

    /// <summary>Converts an IWorkflow to a WorkflowDefinitionDto.</summary>
    public static WorkflowDefinitionDto ToDefinition(IWorkflow workflow)
    {
        return new WorkflowDefinitionDto
        {
            Name = workflow.Name,
            Steps = workflow.Steps.Select(StepInspector.ToDto).ToList()
        };
    }
}
