using FluentAssertions;
using System.Text;
using WorkflowFramework.Extensions.Agents.Mcp;
using Xunit;

namespace WorkflowFramework.Tests.Agents.Mcp;

public class StdioMcpTransportTests
{
    [Fact]
    public void Constructor_NullCommand_Throws()
    {
        var act = () => new StdioMcpTransport(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SendAsync_BeforeConnect_Throws()
    {
        var transport = new StdioMcpTransport("echo");
        var act = async () => await transport.SendAsync(new McpJsonRpcMessage());
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReceiveAsync_BeforeConnect_Throws()
    {
        var transport = new StdioMcpTransport("echo");
        var act = async () => await transport.ReceiveAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Dispose_BeforeConnect_NoOp()
    {
        var transport = new StdioMcpTransport("echo");
        transport.Dispose(); // Should not throw
    }

    [Fact]
    public async Task DisconnectAsync_BeforeConnect_NoOp()
    {
        var transport = new StdioMcpTransport("echo");
        await transport.DisconnectAsync(); // Should not throw
    }

    [Fact]
    public async Task ConnectAsync_InvalidCommand_Throws()
    {
        var transport = new StdioMcpTransport("nonexistent_command_that_does_not_exist_12345");
        var act = () => transport.ConnectAsync();

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ConnectSendReceive_RoundTripsJsonRpcMessage()
    {
        var (command, args) = CreatePowerShellCommand(
            "$line = [Console]::In.ReadLine(); [Console]::Out.WriteLine($line)");
        using var transport = new StdioMcpTransport(command, args);
        await transport.ConnectAsync();

        await transport.SendAsync(new McpJsonRpcMessage { Method = "tools/list" });
        var message = await transport.ReceiveAsync();

        message.Method.Should().Be("tools/list");
    }

    [Fact]
    public async Task ConnectAsync_WithEnvironmentVariables_MakesEnvironmentAvailableToProcess()
    {
        var (command, args) = CreatePowerShellCommand(
            "$value = $env:WF_TEST_ENV; [Console]::Out.WriteLine('{\"jsonrpc\":\"2.0\",\"method\":\"' + $value + '\"}')");
        using var transport = new StdioMcpTransport(command, args, new Dictionary<string, string>
        {
            ["WF_TEST_ENV"] = "env-ready"
        });

        await transport.ConnectAsync();
        var message = await transport.ReceiveAsync();

        message.Method.Should().Be("env-ready");
    }

    [Fact]
    public async Task ReceiveAsync_WithInvalidJson_ThrowsJsonException()
    {
        var (command, args) = CreatePowerShellCommand(
            "[Console]::Out.WriteLine('not-json')");
        using var transport = new StdioMcpTransport(command, args);
        await transport.ConnectAsync();

        var act = async () => await transport.ReceiveAsync();

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    [Fact]
    public async Task ReceiveAsync_WhenStreamEnds_ThrowsInvalidOperationException()
    {
        var (command, args) = CreatePowerShellCommand("exit 0");
        using var transport = new StdioMcpTransport(command, args);
        await transport.ConnectAsync();

        var act = async () => await transport.ReceiveAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*stream ended*");
    }

    [Fact]
    public async Task Dispose_AfterConnect_StopsRunningProcess()
    {
        var (command, args) = CreatePowerShellCommand("Start-Sleep -Seconds 30");
        var transport = new StdioMcpTransport(command, args);

        await transport.ConnectAsync();

        transport.Dispose();
    }

    private static (string Command, string[] Args) CreatePowerShellCommand(string script)
    {
        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return (ResolvePowerShellExecutable(), ["-NoLogo", "-NoProfile", "-EncodedCommand", encodedCommand]);
    }

    private static string ResolvePowerShellExecutable()
    {
        if (TryFindExecutableOnPath("pwsh", out var pwshOnPath))
        {
            return pwshOnPath;
        }

        if (OperatingSystem.IsWindows())
        {
            var windowsCandidates = new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "PowerShell",
                    "7",
                    "pwsh.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell",
                    "v1.0",
                    "powershell.exe")
            };

            var windowsExecutable = windowsCandidates.FirstOrDefault(File.Exists);
            if (windowsExecutable != null)
            {
                return windowsExecutable;
            }

            if (TryFindExecutableOnPath("powershell", out var powershellOnPath))
            {
                return powershellOnPath;
            }
        }

        var unixCandidates = new[] { "/usr/bin/pwsh", "/usr/local/bin/pwsh" };
        var unixExecutable = unixCandidates.FirstOrDefault(File.Exists);
        if (unixExecutable != null)
        {
            return unixExecutable;
        }

        throw new InvalidOperationException("PowerShell executable not found for stdio transport tests.");
    }

    private static bool TryFindExecutableOnPath(string executableName, out string executablePath)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (var path in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(path, executableName);
                if (File.Exists(candidate))
                {
                    executablePath = candidate;
                    return true;
                }

                if (OperatingSystem.IsWindows())
                {
                    var candidateExe = candidate + ".exe";
                    if (File.Exists(candidateExe))
                    {
                        executablePath = candidateExe;
                        return true;
                    }
                }
            }
        }

        executablePath = string.Empty;
        return false;
    }
}
