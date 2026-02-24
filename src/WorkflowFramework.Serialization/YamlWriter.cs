using System.Text;

namespace WorkflowFramework.Serialization;

/// <summary>
/// Simple YAML writer — no external dependencies.
/// </summary>
public static class YamlWriter
{
    public static string Write(WorkflowDefinitionDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"name: {Escape(dto.Name)}");
        sb.AppendLine($"version: {dto.Version}");
        sb.AppendLine("steps:");
        foreach (var step in dto.Steps)
            WriteStep(sb, step, 1);
        return sb.ToString();
    }

    private static void WriteStep(StringBuilder sb, StepDefinitionDto step, int indent)
    {
        var pad = new string(' ', indent * 2);
        sb.AppendLine($"{pad}- name: {Escape(step.Name)}");
        sb.AppendLine($"{pad}  type: {Escape(step.Type)}");

        if (step.MaxAttempts > 0)
            sb.AppendLine($"{pad}  maxAttempts: {step.MaxAttempts}");
        if (step.TimeoutSeconds > 0)
            sb.AppendLine($"{pad}  timeoutSeconds: {step.TimeoutSeconds}");
        if (step.DelaySeconds > 0)
            sb.AppendLine($"{pad}  delaySeconds: {step.DelaySeconds}");
        if (step.SubWorkflowName != null)
            sb.AppendLine($"{pad}  subWorkflowName: {Escape(step.SubWorkflowName)}");

        if (step.Then != null)
        {
            sb.AppendLine($"{pad}  then:");
            WriteStep(sb, step.Then, indent + 2);
        }

        if (step.Else != null)
        {
            sb.AppendLine($"{pad}  else:");
            WriteStep(sb, step.Else, indent + 2);
        }

        if (step.Inner != null)
        {
            sb.AppendLine($"{pad}  inner:");
            WriteStep(sb, step.Inner, indent + 2);
        }

        if (step.Steps is { Count: > 0 })
        {
            sb.AppendLine($"{pad}  steps:");
            foreach (var child in step.Steps)
                WriteStep(sb, child, indent + 2);
        }

        if (step.TryBody is { Count: > 0 })
        {
            sb.AppendLine($"{pad}  tryBody:");
            foreach (var child in step.TryBody)
                WriteStep(sb, child, indent + 2);
        }

        if (step.CatchTypes is { Count: > 0 })
        {
            sb.AppendLine($"{pad}  catchTypes:");
            foreach (var ct in step.CatchTypes)
                sb.AppendLine($"{pad}    - {Escape(ct)}");
        }

        if (step.FinallyBody is { Count: > 0 })
        {
            sb.AppendLine($"{pad}  finallyBody:");
            foreach (var child in step.FinallyBody)
                WriteStep(sb, child, indent + 2);
        }
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains(':') || value.Contains('#') || value.Contains('"') ||
            value.Contains('\n') || value.StartsWith(" ") || value.EndsWith(" "))
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        return value;
    }
}
