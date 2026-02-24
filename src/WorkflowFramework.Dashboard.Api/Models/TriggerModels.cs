namespace WorkflowFramework.Dashboard.Api.Models;

public sealed class TriggerDefinitionDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "";
    public string? Name { get; set; }
    public bool Enabled { get; set; } = true;
    public Dictionary<string, string> Configuration { get; set; } = [];
}

public sealed class TriggerTypeInfoDto
{
    public string Type { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string? ConfigSchema { get; set; }
    public string? Icon { get; set; }
}

public sealed class TriggerStatusDto
{
    public string TriggerId { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTimeOffset? LastFired { get; set; }
    public int FireCount { get; set; }
}

public sealed class SetTriggersRequest
{
    public List<TriggerDefinitionDto> Triggers { get; set; } = [];
}
