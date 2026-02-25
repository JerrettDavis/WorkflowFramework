#if !NETSTANDARD2_0
namespace WorkflowFramework.Triggers.Sources;

/// <summary>
/// Trigger that fires when files are created or changed in a watched directory.
/// Config keys: "path" (required), "filter" (default "*.*"), "includeSubdirectories" (default "false").
/// </summary>
public sealed class FileWatchTriggerSource : ITriggerSource
{
    private readonly TriggerDefinition _definition;
    private FileSystemWatcher? _watcher;
    private TriggerContext? _context;
    private Timer? _debounceTimer;
    private readonly object _debounceLock = new object();
    private string? _pendingPath;
    private string? _pendingChangeType;

    public FileWatchTriggerSource(TriggerDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public string Type => "filewatch";
    public string DisplayName => "File Watcher";
    public bool IsRunning { get; private set; }

    public Task StartAsync(TriggerContext context, CancellationToken ct = default)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        _context = context;

        var config = context.Configuration;
        if (!config.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("FileWatchTriggerSource requires 'path' in configuration.");

        config.TryGetValue("filter", out var filter);
        if (string.IsNullOrWhiteSpace(filter)) filter = "*.*";

        config.TryGetValue("includeSubdirectories", out var subDirs);
        var includeSubDirs = string.Equals(subDirs, "true", StringComparison.OrdinalIgnoreCase);

        _watcher = new FileSystemWatcher(path, filter)
        {
            IncludeSubdirectories = includeSubDirs,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        _watcher.Created += OnFileEvent;
        _watcher.Changed += OnFileEvent;

        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileEvent;
            _watcher.Changed -= OnFileEvent;
        }
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        IsRunning = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _watcher?.Dispose();
        _watcher = null;
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        IsRunning = false;
        return default;
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        lock (_debounceLock)
        {
            _pendingPath = e.FullPath;
            _pendingChangeType = e.ChangeType.ToString();
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(FireDebounced, null, 500, Timeout.Infinite);
        }
    }

    private async void FireDebounced(object? state)
    {
        try
        {
            string? filePath, changeType;
            lock (_debounceLock)
            {
                filePath = _pendingPath;
                changeType = _pendingChangeType;
                _pendingPath = null;
                _pendingChangeType = null;
            }

            if (filePath is null || _context is null) return;

            await _context.OnTriggered(new TriggerEvent
            {
                TriggerType = Type,
                Timestamp = DateTimeOffset.UtcNow,
                Payload = new Dictionary<string, object>
                {
                    ["filePath"] = filePath,
                    ["fileName"] = Path.GetFileName(filePath),
                    ["changeType"] = changeType ?? "Unknown"
                }
            }).ConfigureAwait(false);
        }
        catch
        {
            // Swallow
        }
    }
}

#endif
