namespace WorkflowFramework.Serialization;

/// <summary>
/// Simple YAML reader — no external dependencies. Handles the subset produced by YamlWriter.
/// </summary>
internal static class YamlReader
{
    public static WorkflowDefinitionDto Read(string yaml)
    {
        var lines = yaml.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
            .ToList();

        var dto = new WorkflowDefinitionDto();
        var i = 0;

        while (i < lines.Count)
        {
            var (key, value) = ParseKv(lines[i]);
            switch (key)
            {
                case "name":
                    dto.Name = Unescape(value);
                    i++;
                    break;
                case "version":
                    dto.Version = int.TryParse(value, out var v) ? v : 1;
                    i++;
                    break;
                case "steps":
                    i++;
                    dto.Steps = ReadStepList(lines, ref i, Indent(lines[i - 1]) + 2);
                    break;
                default:
                    i++;
                    break;
            }
        }

        return dto;
    }

    private static List<StepDefinitionDto> ReadStepList(List<string> lines, ref int i, int expectedIndent)
    {
        var steps = new List<StepDefinitionDto>();
        while (i < lines.Count)
        {
            var indent = Indent(lines[i]);
            if (indent < expectedIndent) break;

            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("- "))
            {
                var step = ReadStep(lines, ref i, expectedIndent);
                steps.Add(step);
            }
            else
            {
                break;
            }
        }
        return steps;
    }

    private static StepDefinitionDto ReadStep(List<string> lines, ref int i, int listIndent)
    {
        var step = new StepDefinitionDto();
        // First line is "- key: value"
        var firstLine = lines[i].TrimStart();
        var afterDash = firstLine[2..]; // skip "- "
        var (k0, v0) = ParseKv(afterDash);
        SetStepProperty(step, k0, v0);
        i++;

        var propIndent = listIndent + 2; // properties are indented 2 more than the dash

        while (i < lines.Count)
        {
            var indent = Indent(lines[i]);
            if (indent < propIndent) break;
            if (indent > propIndent)
            {
                // Sub-content already consumed
                break;
            }

            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("- ")) break; // next list item at same level

            var (key, value) = ParseKv(trimmed);

            switch (key)
            {
                case "steps":
                    i++;
                    step.Steps = ReadStepList(lines, ref i, propIndent + 2);
                    break;
                case "then":
                    i++;
                    var thenList = ReadStepList(lines, ref i, propIndent + 2);
                    step.Then = thenList.Count > 0 ? thenList[0] : null;
                    break;
                case "else":
                    i++;
                    var elseList = ReadStepList(lines, ref i, propIndent + 2);
                    step.Else = elseList.Count > 0 ? elseList[0] : null;
                    break;
                case "inner":
                    i++;
                    var innerList = ReadStepList(lines, ref i, propIndent + 2);
                    step.Inner = innerList.Count > 0 ? innerList[0] : null;
                    break;
                case "tryBody":
                    i++;
                    step.TryBody = ReadStepList(lines, ref i, propIndent + 2);
                    break;
                case "finallyBody":
                    i++;
                    step.FinallyBody = ReadStepList(lines, ref i, propIndent + 2);
                    break;
                case "catchTypes":
                    i++;
                    step.CatchTypes = ReadStringList(lines, ref i, propIndent + 2);
                    break;
                default:
                    SetStepProperty(step, key, value);
                    i++;
                    break;
            }
        }

        return step;
    }

    private static List<string> ReadStringList(List<string> lines, ref int i, int expectedIndent)
    {
        var items = new List<string>();
        while (i < lines.Count)
        {
            var indent = Indent(lines[i]);
            if (indent < expectedIndent) break;

            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("- "))
            {
                items.Add(Unescape(trimmed[2..].Trim()));
                i++;
            }
            else
            {
                break;
            }
        }
        return items;
    }

    private static void SetStepProperty(StepDefinitionDto step, string key, string value)
    {
        switch (key)
        {
            case "name": step.Name = Unescape(value); break;
            case "type": step.Type = Unescape(value); break;
            case "maxAttempts": step.MaxAttempts = int.TryParse(value, out var ma) ? ma : 0; break;
            case "timeoutSeconds": step.TimeoutSeconds = double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ts) ? ts : 0; break;
            case "delaySeconds": step.DelaySeconds = double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ds) ? ds : 0; break;
            case "subWorkflowName": step.SubWorkflowName = Unescape(value); break;
        }
    }

    private static (string key, string value) ParseKv(string line)
    {
        var colonIdx = line.IndexOf(':');
        if (colonIdx < 0) return (line.Trim(), "");
        var key = line[..colonIdx].Trim();
        var value = line[(colonIdx + 1)..].Trim();
        return (key, value);
    }

    private static string Unescape(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
        return value;
    }

    private static int Indent(string line)
    {
        var count = 0;
        foreach (var c in line)
        {
            if (c == ' ') count++;
            else break;
        }
        return count;
    }
}
