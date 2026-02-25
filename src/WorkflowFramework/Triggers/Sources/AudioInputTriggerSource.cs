#if !NETSTANDARD2_0
namespace WorkflowFramework.Triggers.Sources;

/// <summary>
/// Specialized file watcher for audio file drops.
/// Config keys: "watchPath" (required), "formats" (default "wav,mp3,m4a,webm,ogg,flac").
/// </summary>
public sealed class AudioInputTriggerSource : ITriggerSource
{
    private static readonly HashSet<string> DefaultFormats = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    { ".wav", ".mp3", ".m4a", ".webm", ".ogg", ".flac" };

    private readonly TriggerDefinition _definition;
    private FileSystemWatcher? _watcher;
    private TriggerContext? _context;
    private HashSet<string>? _formats;
    private Timer? _debounceTimer;
    private readonly object _debounceLock = new object();
    private string? _pendingPath;

    public AudioInputTriggerSource(TriggerDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public string Type => "audio";
    public string DisplayName => "Audio Input";
    public bool IsRunning { get; private set; }

    public Task StartAsync(TriggerContext context, CancellationToken ct = default)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        _context = context;

        var config = context.Configuration;
        if (!config.TryGetValue("watchPath", out var watchPath) || string.IsNullOrWhiteSpace(watchPath))
            throw new InvalidOperationException("AudioInputTriggerSource requires 'watchPath' in configuration.");

        _formats = DefaultFormats;
        if (config.TryGetValue("formats", out var fmts) && !string.IsNullOrWhiteSpace(fmts))
        {
            _formats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in fmts.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var ext = f.Trim();
                if (!ext.StartsWith(".")) ext = "." + ext;
                _formats.Add(ext);
            }
        }

        _watcher = new FileSystemWatcher(watchPath, "*.*")
        {
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
        };
        _watcher.Created += OnFileCreated;

        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileCreated;
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

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        var ext = Path.GetExtension(e.FullPath);
        if (_formats is null || !_formats.Contains(ext)) return;

        lock (_debounceLock)
        {
            _pendingPath = e.FullPath;
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(FireDebounced, null, 500, Timeout.Infinite);
        }
    }

    private async void FireDebounced(object? state)
    {
        try
        {
            string? filePath;
            lock (_debounceLock)
            {
                filePath = _pendingPath;
                _pendingPath = null;
            }

            if (filePath is null || _context is null) return;

            long fileSize = 0;
            try { fileSize = new FileInfo(filePath).Length; } catch { /* ignore */ }

            await _context.OnTriggered(new TriggerEvent
            {
                TriggerType = Type,
                Timestamp = DateTimeOffset.UtcNow,
                Payload = new Dictionary<string, object>
                {
                    ["filePath"] = filePath,
                    ["fileName"] = Path.GetFileName(filePath),
                    ["format"] = Path.GetExtension(filePath).TrimStart('.'),
                    ["fileSizeBytes"] = fileSize
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
