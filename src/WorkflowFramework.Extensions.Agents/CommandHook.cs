using System.Diagnostics;
using System.Text.Json;

namespace WorkflowFramework.Extensions.Agents;

/// <summary>Runs a shell command, passes HookContext JSON on stdin, reads HookResult JSON from stdout.</summary>
public sealed class CommandHook : IAgentHook
{
    private readonly string _command;
    private readonly string[]? _args;

    /// <summary>Initializes a new CommandHook.</summary>
    public CommandHook(string command, string[]? args = null, string? matcher = null, TimeSpan? timeout = null)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _args = args;
        Matcher = matcher;
        Timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    /// <inheritdoc />
    public string? Matcher { get; }

    /// <summary>Gets or sets the timeout.</summary>
    public TimeSpan Timeout { get; set; }

    /// <inheritdoc />
    public async Task<HookResult> ExecuteAsync(AgentHookEvent hookEvent, HookContext context, CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _command,
            Arguments = _args != null ? string.Join(" ", _args) : string.Empty,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
            return HookResult.DenyResult("Failed to start command: " + _command);

        var inputJson = SerializeContext(hookEvent, context);
        await process.StandardInput.WriteAsync(inputJson).ConfigureAwait(false);
        process.StandardInput.Close();

        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);

#if NET8_0_OR_GREATER
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
#else
        process.WaitForExit((int)Timeout.TotalMilliseconds);
#endif

        if (process.ExitCode != 0)
            return HookResult.DenyResult("Command exited with code " + process.ExitCode);

        try
        {
            return DeserializeResult(output) ?? HookResult.AllowResult();
        }
        catch
        {
            return HookResult.AllowResult();
        }
    }

    internal static string SerializeContext(AgentHookEvent hookEvent, HookContext context)
    {
        var obj = new Dictionary<string, object?>
        {
            ["event"] = hookEvent.ToString(),
            ["stepName"] = context.StepName,
            ["toolName"] = context.ToolName,
            ["toolArgs"] = context.ToolArgs
        };
        return JsonSerializer.Serialize(obj);
    }

    internal static HookResult? DeserializeResult(string json)
    {
        return JsonSerializer.Deserialize<HookResult>(json);
    }
}
