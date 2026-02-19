using System.Threading.Channels;
using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Sources;

/// <summary>
/// Watches a directory for new .txt/.md files and yields them as source messages.
/// </summary>
public sealed class FileWatcherTaskSource : ITaskSource, IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Channel<SourceMessage> _channel = Channel.CreateUnbounded<SourceMessage>();

    /// <summary>Initializes a new instance watching the given directory.</summary>
    public FileWatcherTaskSource(string directory)
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        _watcher = new FileSystemWatcher(directory)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        _watcher.Created += OnFileCreated;
    }

    /// <inheritdoc />
    public string Name => "FileWatcher";

    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (!e.Name!.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
            !e.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return;

        // Small delay to let the file finish writing
        await Task.Delay(200);

        try
        {
            var content = await File.ReadAllTextAsync(e.FullPath);
            await _channel.Writer.WriteAsync(new SourceMessage
            {
                Source = "file",
                RawContent = content,
                Metadata = { ["filename"] = e.Name, ["path"] = e.FullPath }
            });
        }
        catch
        {
            // Swallow file read errors in the demo
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceMessage> GetMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_channel.Reader.TryRead(out var message))
            {
                yield return message;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _channel.Writer.TryComplete();
    }
}
