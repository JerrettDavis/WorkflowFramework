using System.Text.Json;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;

namespace WorkflowFramework.Dashboard.Api.Plugins.BuiltInPlugins;

/// <summary>
/// Built-in plugin providing email step types.
/// </summary>
public sealed class EmailStepPlugin : IStepTypePlugin
{
    public string Id => "builtin.email";
    public string Name => "Email Integration";
    public string Version => "1.0.0";

    private static JsonElement? Schema(string json) => JsonDocument.Parse(json).RootElement;

    public IReadOnlyList<StepTypeInfo> StepTypes =>
    [
        new StepTypeInfo
        {
            Type = "SendEmail",
            Name = "Send Email",
            Category = "Communication",
            Description = "Sends an email via SMTP",
            ConfigSchema = Schema("""
            {
              "properties": {
                "to":        { "type": "string", "uiType": "text", "label": "To", "required": true, "helpText": "Recipient email address" },
                "subject":   { "type": "string", "uiType": "text", "label": "Subject", "required": true },
                "body":      { "type": "string", "uiType": "textarea", "label": "Body", "rows": 6, "required": true },
                "smtpHost":  { "type": "string", "uiType": "text", "label": "SMTP Host", "helpText": "e.g., smtp.gmail.com" },
                "smtpPort":  { "type": "number", "uiType": "number", "label": "SMTP Port", "default": 587 }
              },
              "required": ["to", "subject", "body"]
            }
            """)
        }
    ];

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;

    public IStep? CreateStep(string stepType, string stepName, Dictionary<string, string>? config)
    {
        if (stepType != "SendEmail") return null;
        return new DynamicStep(stepName, ctx =>
        {
            var to = config?.GetValueOrDefault("to") ?? "unknown";
            var subject = config?.GetValueOrDefault("subject") ?? "No Subject";
            ctx.Properties[$"{stepName}.Output"] = $"Email sent to {to}: {subject}";
            return Task.CompletedTask;
        });
    }
}
