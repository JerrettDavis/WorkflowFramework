using WorkflowFramework.Dashboard.Api.Models;

namespace WorkflowFramework.Dashboard.Api.Services;

public sealed class TriggerTypeRegistry
{
    private readonly List<TriggerTypeInfoDto> _types = [];

    public static TriggerTypeRegistry CreateDefault()
    {
        var registry = new TriggerTypeRegistry();

        registry.Register(new TriggerTypeInfoDto
        {
            Type = "manual",
            DisplayName = "Manual (API/UI)",
            Description = "Triggered manually via the dashboard or API call.",
            Category = "Basic",
            Icon = "▶️",
            ConfigSchema = """{"properties":{"inputSchema":{"type":"string","uiType":"textarea","label":"Input Schema (JSON)","helpText":"Optional JSON schema defining required input fields","rows":4}}}"""
        });

        registry.Register(new TriggerTypeInfoDto
        {
            Type = "schedule",
            DisplayName = "Schedule (Cron)",
            Description = "Runs on a recurring schedule using cron expressions.",
            Category = "Time",
            Icon = "⏰",
            ConfigSchema = """{"properties":{"cronExpression":{"type":"string","uiType":"text","label":"Cron Expression","required":true,"helpText":"e.g., */5 * * * * (every 5 min), 0 9 * * 1-5 (weekdays 9am)","placeholder":"* * * * *"},"timezone":{"type":"string","uiType":"text","label":"Timezone","helpText":"e.g., America/Chicago. Defaults to UTC.","placeholder":"UTC"}},"required":["cronExpression"]}"""
        });

        registry.Register(new TriggerTypeInfoDto
        {
            Type = "webhook",
            DisplayName = "Webhook",
            Description = "Triggered by an incoming HTTP POST request.",
            Category = "HTTP",
            Icon = "🔗",
            ConfigSchema = """{"properties":{"secret":{"type":"string","uiType":"password","label":"Webhook Secret","helpText":"Optional HMAC secret for payload verification"},"method":{"type":"string","uiType":"select","label":"HTTP Method","options":["POST","PUT","PATCH"],"default":"POST"},"contentType":{"type":"string","uiType":"select","label":"Content Type","options":["application/json","application/xml","text/plain","multipart/form-data"],"default":"application/json"}}}"""
        });

        registry.Register(new TriggerTypeInfoDto
        {
            Type = "filewatch",
            DisplayName = "File Watcher",
            Description = "Monitors a directory for new or changed files.",
            Category = "I/O",
            Icon = "📁",
            ConfigSchema = """{"properties":{"path":{"type":"string","uiType":"text","label":"Watch Directory","required":true,"helpText":"Directory path to monitor","placeholder":"C:\\data\\incoming"},"filter":{"type":"string","uiType":"text","label":"File Filter","helpText":"e.g., *.json, *.csv, *.*","default":"*.*","placeholder":"*.*"},"includeSubdirectories":{"type":"boolean","uiType":"checkbox","label":"Include Subdirectories","default":false},"changeTypes":{"type":"string","uiType":"multiselect","label":"Change Types","options":["Created","Changed","Renamed"],"default":["Created"]}},"required":["path"]}"""
        });

        registry.Register(new TriggerTypeInfoDto
        {
            Type = "audio",
            DisplayName = "Audio Input",
            Description = "Watches for audio file drops (wav, mp3, m4a, webm, etc.).",
            Category = "I/O",
            Icon = "🎤",
            ConfigSchema = """{"properties":{"watchPath":{"type":"string","uiType":"text","label":"Watch Directory","required":true,"helpText":"Directory to monitor for audio files","placeholder":"C:\\recordings"},"formats":{"type":"string","uiType":"text","label":"Audio Formats","helpText":"Comma-separated extensions","default":"wav,mp3,m4a,webm,ogg,flac","placeholder":"wav,mp3,m4a"}},"required":["watchPath"]}"""
        });

        registry.Register(new TriggerTypeInfoDto
        {
            Type = "queue",
            DisplayName = "Message Queue",
            Description = "Triggered by messages from a configured message queue/topic.",
            Category = "Integration",
            Icon = "📨",
            ConfigSchema = """{"properties":{"source":{"type":"string","uiType":"text","label":"Queue/Topic Name","required":true,"helpText":"The queue or topic to subscribe to","placeholder":"my-workflow-queue"},"connectorType":{"type":"string","uiType":"select","label":"Connector","options":["InMemory","RabbitMQ","AzureServiceBus","Kafka"],"default":"InMemory","helpText":"Message broker connector to use"}},"required":["source"]}"""
        });

        return registry;
    }

    public void Register(TriggerTypeInfoDto info) => _types.Add(info);
    public IReadOnlyList<TriggerTypeInfoDto> GetAll() => _types;
    public TriggerTypeInfoDto? GetByType(string type) => _types.FirstOrDefault(t => t.Type == type);
}
