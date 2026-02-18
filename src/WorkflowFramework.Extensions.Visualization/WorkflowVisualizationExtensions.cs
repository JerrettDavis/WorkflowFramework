using System.Text;

namespace WorkflowFramework.Extensions.Visualization;

/// <summary>
/// Extension methods for exporting workflow definitions to visualization formats.
/// </summary>
public static class WorkflowVisualizationExtensions
{
    /// <summary>
    /// Exports the workflow to Mermaid diagram format.
    /// </summary>
    /// <param name="workflow">The workflow to visualize.</param>
    /// <returns>A Mermaid diagram string.</returns>
    public static string ToMermaid(this IWorkflow workflow)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph TD");

        var steps = workflow.Steps;
        if (steps.Count == 0)
        {
            sb.AppendLine("    Start([Start]) --> End([End])");
            return sb.ToString();
        }

        sb.AppendLine("    Start([Start])");
        var prevId = "Start";

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var id = SanitizeId($"S{i}_{step.Name}");
            var label = EscapeMermaidLabel(step.Name);

            if (step.Name.StartsWith("If("))
            {
                sb.AppendLine($"    {id}{{{{{label}}}}}");
            }
            else if (step.Name.StartsWith("Parallel("))
            {
                sb.AppendLine($"    {id}[/{label}\\]");
            }
            else
            {
                sb.AppendLine($"    {id}[{label}]");
            }

            sb.AppendLine($"    {prevId} --> {id}");
            prevId = id;
        }

        sb.AppendLine("    End([End])");
        sb.AppendLine($"    {prevId} --> End");

        return sb.ToString();
    }

    /// <summary>
    /// Exports the workflow to DOT (Graphviz) format.
    /// </summary>
    /// <param name="workflow">The workflow to visualize.</param>
    /// <returns>A DOT format string.</returns>
    public static string ToDot(this IWorkflow workflow)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"digraph \"{EscapeDotLabel(workflow.Name)}\" {{");
        sb.AppendLine("    rankdir=TB;");
        sb.AppendLine("    node [shape=box, style=rounded];");
        sb.AppendLine("    Start [shape=circle, label=\"\", width=0.3, style=filled, fillcolor=black];");
        sb.AppendLine("    End [shape=doublecircle, label=\"\", width=0.3, style=filled, fillcolor=black];");

        var steps = workflow.Steps;
        if (steps.Count == 0)
        {
            sb.AppendLine("    Start -> End;");
            sb.AppendLine("}");
            return sb.ToString();
        }

        var prevId = "Start";

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var id = SanitizeId($"S{i}_{step.Name}");
            var label = EscapeDotLabel(step.Name);

            if (step.Name.StartsWith("If("))
                sb.AppendLine($"    {id} [label=\"{label}\", shape=diamond];");
            else if (step.Name.StartsWith("Parallel("))
                sb.AppendLine($"    {id} [label=\"{label}\", shape=parallelogram];");
            else
                sb.AppendLine($"    {id} [label=\"{label}\"];");

            sb.AppendLine($"    {prevId} -> {id};");
            prevId = id;
        }

        sb.AppendLine($"    {prevId} -> End;");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string SanitizeId(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
            else
                sb.Append('_');
        }
        return sb.ToString();
    }

    private static string EscapeMermaidLabel(string label) =>
        label.Replace("\"", "&quot;").Replace("[", "&#91;").Replace("]", "&#93;");

    private static string EscapeDotLabel(string label) =>
        label.Replace("\"", "\\\"").Replace("\\", "\\\\");
}
