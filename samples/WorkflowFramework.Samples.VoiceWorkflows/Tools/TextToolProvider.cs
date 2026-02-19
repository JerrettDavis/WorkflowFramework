using System.Text.Json;
using System.Text.RegularExpressions;
using WorkflowFramework.Extensions.Agents;

namespace WorkflowFramework.Samples.VoiceWorkflows.Tools;

/// <summary>Text manipulation tool provider with real implementations.</summary>
public sealed class TextToolProvider : IToolProvider
{
    public Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken ct = default)
    {
        var tools = new List<ToolDefinition>
        {
            new()
            {
                Name = "chunk_text",
                Description = "Split text into chunks by character limit with overlap.",
                ParametersSchema = """{"type":"object","properties":{"text":{"type":"string"},"max_chars":{"type":"integer"},"overlap":{"type":"integer"}},"required":["text","max_chars"]}"""
            },
            new()
            {
                Name = "merge_texts",
                Description = "Combine multiple texts with a separator.",
                ParametersSchema = """{"type":"object","properties":{"texts":{"type":"array","items":{"type":"string"}},"separator":{"type":"string"}},"required":["texts"]}"""
            },
            new()
            {
                Name = "regex_replace",
                Description = "Apply a regex pattern replacement to text.",
                ParametersSchema = """{"type":"object","properties":{"text":{"type":"string"},"pattern":{"type":"string"},"replacement":{"type":"string"}},"required":["text","pattern","replacement"]}"""
            },
            new()
            {
                Name = "extract_json",
                Description = "Extract JSON from text, optionally by a dot-separated path.",
                ParametersSchema = """{"type":"object","properties":{"text":{"type":"string"},"path":{"type":"string"}},"required":["text"]}"""
            }
        };
        return Task.FromResult<IReadOnlyList<ToolDefinition>>(tools);
    }

    public Task<ToolResult> InvokeToolAsync(string toolName, string argumentsJson, CancellationToken ct = default)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        return toolName switch
        {
            "chunk_text" => Task.FromResult(ChunkText(args)),
            "merge_texts" => Task.FromResult(MergeTexts(args)),
            "regex_replace" => Task.FromResult(RegexReplace(args)),
            "extract_json" => Task.FromResult(ExtractJson(args)),
            _ => Task.FromResult(new ToolResult { Content = $"Unknown tool: {toolName}", IsError = true })
        };
    }

    private static ToolResult ChunkText(JsonElement args)
    {
        var text = args.GetProperty("text").GetString() ?? "";
        var maxChars = args.GetProperty("max_chars").GetInt32();
        var overlap = args.TryGetProperty("overlap", out var ov) ? ov.GetInt32() : 0;

        var chunks = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            var end = Math.Min(i + maxChars, text.Length);
            chunks.Add(text[i..end]);
            i += maxChars - overlap;
            if (i >= text.Length) break;
        }

        return new ToolResult { Content = JsonSerializer.Serialize(new { chunks, count = chunks.Count }) };
    }

    private static ToolResult MergeTexts(JsonElement args)
    {
        var texts = args.GetProperty("texts").EnumerateArray().Select(e => e.GetString() ?? "").ToList();
        var sep = args.TryGetProperty("separator", out var s) ? s.GetString() ?? "\n" : "\n";
        return new ToolResult { Content = string.Join(sep, texts) };
    }

    private static ToolResult RegexReplace(JsonElement args)
    {
        var text = args.GetProperty("text").GetString() ?? "";
        var pattern = args.GetProperty("pattern").GetString() ?? "";
        var replacement = args.GetProperty("replacement").GetString() ?? "";
        try
        {
            return new ToolResult { Content = Regex.Replace(text, pattern, replacement) };
        }
        catch (Exception ex)
        {
            return new ToolResult { Content = ex.Message, IsError = true };
        }
    }

    private static ToolResult ExtractJson(JsonElement args)
    {
        var text = args.GetProperty("text").GetString() ?? "";
        // Find JSON in text (first { to matching })
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end < start)
        {
            start = text.IndexOf('[');
            end = text.LastIndexOf(']');
        }
        if (start < 0 || end < start)
            return new ToolResult { Content = "No JSON found in text", IsError = true };

        var json = text[start..(end + 1)];
        if (args.TryGetProperty("path", out var pathEl))
        {
            var path = pathEl.GetString();
            if (!string.IsNullOrEmpty(path))
            {
                var doc = JsonSerializer.Deserialize<JsonElement>(json);
                foreach (var segment in path.Split('.'))
                {
                    if (doc.TryGetProperty(segment, out var child))
                        doc = child;
                    else
                        return new ToolResult { Content = $"Path segment '{segment}' not found", IsError = true };
                }
                return new ToolResult { Content = doc.ToString() };
            }
        }
        return new ToolResult { Content = json };
    }
}
