using System.Diagnostics;
using System.Text.Json;

namespace WorkflowFramework.Extensions.Agents.Mcp;

/// <summary>
/// MCP transport that communicates via stdin/stdout of a child process.
/// </summary>
public sealed class StdioMcpTransport : IMcpTransport
{
    private readonly string _command;
    private readonly string[] _args;
    private readonly IDictionary<string, string>? _env;
    private Process? _process;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="StdioMcpTransport"/>.
    /// </summary>
    public StdioMcpTransport(string command, string[]? args = null, IDictionary<string, string>? env = null)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _args = args ?? Array.Empty<string>();
        _env = env;
    }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _command,
            Arguments = string.Join(" ", _args),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (_env != null)
        {
            foreach (var kvp in _env)
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }
        }

        _process = Process.Start(startInfo);
        if (_process == null)
            throw new InvalidOperationException($"Failed to start process: {_command}");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.StandardInput.Close();
                if (!_process.WaitForExit(3000))
                {
                    _process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendAsync(McpJsonRpcMessage message, CancellationToken ct = default)
    {
        if (_process == null) throw new InvalidOperationException("Transport not connected.");
        var json = JsonSerializer.Serialize(message);
        await _process.StandardInput.WriteLineAsync(json).ConfigureAwait(false);
        await _process.StandardInput.FlushAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<McpJsonRpcMessage> ReceiveAsync(CancellationToken ct = default)
    {
        if (_process == null) throw new InvalidOperationException("Transport not connected.");
        var line = await _process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
        if (line == null)
            throw new InvalidOperationException("Transport stream ended.");
        return JsonSerializer.Deserialize<McpJsonRpcMessage>(line)
               ?? throw new InvalidOperationException("Failed to deserialize JSON-RPC message.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_process != null && !_process.HasExited)
            {
                try { _process.Kill(); } catch { }
            }
            _process?.Dispose();
        }
    }
}
