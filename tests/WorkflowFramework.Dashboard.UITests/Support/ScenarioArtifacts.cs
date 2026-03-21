using System.Text.Json;

namespace WorkflowFramework.Dashboard.UITests.Support;

public sealed class ScenarioArtifacts
{
    public ScenarioArtifacts(string title, string directory, IReadOnlyList<string> tags)
    {
        Title = title;
        Directory = directory;
        Tags = tags;
    }

    public string Title { get; }
    public string Directory { get; }
    public IReadOnlyList<string> Tags { get; }
    public List<ScenarioArtifactItem> Items { get; } = [];

    public void Add(string kind, string path, string? promotedPath = null)
    {
        Items.Add(new ScenarioArtifactItem(kind, path, promotedPath));
    }

    public Task WriteManifestAsync(bool passed)
    {
        var manifestPath = Path.Combine(Directory, "manifest.json");
        var payload = new
        {
            title = Title,
            passed,
            tags = Tags,
            artifacts = Items.Select(item => new
            {
                kind = item.Kind,
                path = item.Path,
                promotedPath = item.PromotedPath
            }).ToArray()
        };

        return File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}

public sealed record ScenarioArtifactItem(string Kind, string Path, string? PromotedPath);
