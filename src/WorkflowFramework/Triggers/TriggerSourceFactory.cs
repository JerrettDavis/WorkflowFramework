#if !NETSTANDARD2_0
namespace WorkflowFramework.Triggers;

/// <summary>
/// Default implementation of <see cref="ITriggerSourceFactory"/> that supports
/// registering trigger type factories and creating instances from definitions.
/// </summary>
public class TriggerSourceFactory : ITriggerSourceFactory
{
    private readonly Dictionary<string, Func<TriggerDefinition, ITriggerSource>> _factories =
        new Dictionary<string, Func<TriggerDefinition, ITriggerSource>>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, TriggerTypeInfo> _typeInfos =
        new Dictionary<string, TriggerTypeInfo>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a factory with built-in trigger types pre-registered.
    /// </summary>
    public TriggerSourceFactory()
    {
        RegisterBuiltInTriggers();
    }

    /// <summary>
    /// Registers a trigger type factory.
    /// </summary>
    public void Register(string type, Func<TriggerDefinition, ITriggerSource> factory, TriggerTypeInfo? info = null)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (factory is null) throw new ArgumentNullException(nameof(factory));

        _factories[type] = factory;
        if (info is not null)
            _typeInfos[type] = info;
    }

    /// <inheritdoc />
    public ITriggerSource Create(TriggerDefinition definition)
    {
        if (definition is null) throw new ArgumentNullException(nameof(definition));

        if (!_factories.TryGetValue(definition.Type, out var factory))
            throw new InvalidOperationException($"Unknown trigger type: '{definition.Type}'. Registered types: {string.Join(", ", _factories.Keys)}");

        return factory(definition);
    }

    /// <inheritdoc />
    public IReadOnlyList<TriggerTypeInfo> GetAvailableTypes()
    {
        return new List<TriggerTypeInfo>(_typeInfos.Values);
    }

    private void RegisterBuiltInTriggers()
    {
        Register("schedule", def => new Sources.ScheduleTriggerSource(def), new TriggerTypeInfo
        {
            Type = "schedule",
            DisplayName = "Schedule (Cron)",
            Description = "Fires on a cron schedule.",
            Category = "Time",
            ConfigSchema = @"{""properties"":{""cronExpression"":{""type"":""string"",""description"":""Cron expression (5-field)""},""timezone"":{""type"":""string"",""description"":""Timezone ID (optional)""}},""required"":[""cronExpression""]}",
            Icon = "clock"
        });

        Register("filewatch", def => new Sources.FileWatchTriggerSource(def), new TriggerTypeInfo
        {
            Type = "filewatch",
            DisplayName = "File Watcher",
            Description = "Fires when files are created or changed in a directory.",
            Category = "File",
            ConfigSchema = @"{""properties"":{""path"":{""type"":""string"",""description"":""Directory to watch""},""filter"":{""type"":""string"",""description"":""File filter (e.g. *.json)""},""includeSubdirectories"":{""type"":""string"",""enum"":[""true"",""false""]}},""required"":[""path""]}",
            Icon = "folder"
        });

        Register("manual", def => new Sources.ManualTriggerSource(def), new TriggerTypeInfo
        {
            Type = "manual",
            DisplayName = "Manual (API/UI)",
            Description = "Fires when manually triggered via API or UI.",
            Category = "Manual",
            ConfigSchema = @"{""properties"":{""inputSchema"":{""type"":""string"",""description"":""JSON schema for required input""}}}",
            Icon = "play"
        });

        Register("audio", def => new Sources.AudioInputTriggerSource(def), new TriggerTypeInfo
        {
            Type = "audio",
            DisplayName = "Audio Input",
            Description = "Fires when audio files are dropped in a watched directory.",
            Category = "File",
            ConfigSchema = @"{""properties"":{""watchPath"":{""type"":""string"",""description"":""Directory to watch""},""formats"":{""type"":""string"",""description"":""Comma-separated formats (e.g. wav,mp3)""}},""required"":[""watchPath""]}",
            Icon = "microphone"
        });
    }
}

#endif
