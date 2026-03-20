using System.Text;
using System.Text.Json;
using FluentAssertions;
using WorkflowFramework.Extensions.Agents;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class CommandHookTests
{
    [Fact]
    public void Constructor_NullCommand_Throws()
    {
        var act = () => new CommandHook(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Matcher_ReturnsProvidedValue()
    {
        var hook = new CommandHook("cmd", matcher: "tool*");
        hook.Matcher.Should().Be("tool*");
    }

    [Fact]
    public void Matcher_DefaultIsNull()
    {
        var hook = new CommandHook("cmd");
        hook.Matcher.Should().BeNull();
    }

    [Fact]
    public void Timeout_DefaultIs30Seconds()
    {
        var hook = new CommandHook("cmd");
        hook.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Timeout_CustomValue()
    {
        var hook = new CommandHook("cmd", timeout: TimeSpan.FromSeconds(60));
        hook.Timeout.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void HookContext_JsonRoundTrip()
    {
        var context = new HookContext
        {
            Event = AgentHookEvent.PreToolCall,
            StepName = "step1",
            ToolName = "myTool",
            ToolArgs = "{\"key\":\"val\"}"
        };

        var json = JsonSerializer.Serialize(context);
        var deserialized = JsonSerializer.Deserialize<HookContext>(json);

        deserialized.Should().NotBeNull();
        deserialized!.StepName.Should().Be("step1");
        deserialized.ToolName.Should().Be("myTool");
        deserialized.ToolArgs.Should().Be("{\"key\":\"val\"}");
    }

    [Fact]
    public void HookResult_JsonRoundTrip()
    {
        var result = HookResult.DenyResult("not allowed");
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<HookResult>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Decision.Should().Be(HookDecision.Deny);
        deserialized.Reason.Should().Be("not allowed");
    }

    [Fact]
    public void HookResult_AllowResult_JsonRoundTrip()
    {
        var result = HookResult.AllowResult("ok");
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<HookResult>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Decision.Should().Be(HookDecision.Allow);
        deserialized.Reason.Should().Be("ok");
    }

    [Fact]
    public void HookResult_ModifyResult_JsonRoundTrip()
    {
        var result = HookResult.ModifyResult("{\"modified\":true}", "changed args");
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<HookResult>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Decision.Should().Be(HookDecision.Modify);
        deserialized.ModifiedArgs.Should().Be("{\"modified\":true}");
        deserialized.Reason.Should().Be("changed args");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandReturnsHookResult_DeserializesResponse()
    {
        var (command, args) = CreatePowerShellCommand(
            "$null = [Console]::In.ReadToEnd(); [Console]::Out.Write('{\"Decision\":2,\"Reason\":\"rewritten\",\"ModifiedArgs\":\"{\\\"safe\\\":true}\"}')");
        var hook = new CommandHook(command, args);

        var result = await hook.ExecuteAsync(AgentHookEvent.PreToolCall, new HookContext
        {
            StepName = "Planner",
            ToolName = "search",
            ToolArgs = "{\"q\":\"incident\"}"
        });

        result.Decision.Should().Be(HookDecision.Modify);
        result.Reason.Should().Be("rewritten");
        result.ModifiedArgs.Should().Be("{\"safe\":true}");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandExitsNonZero_ReturnsDeny()
    {
        var (command, args) = CreatePowerShellCommand("exit 7");
        var hook = new CommandHook(command, args);

        var result = await hook.ExecuteAsync(AgentHookEvent.PreToolCall, new HookContext());

        result.Decision.Should().Be(HookDecision.Deny);
        result.Reason.Should().Be("Command exited with code 7");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandReturnsInvalidJson_ReturnsAllow()
    {
        var (command, args) = CreatePowerShellCommand(
            "$null = [Console]::In.ReadToEnd(); [Console]::Out.Write('not-json')");
        var hook = new CommandHook(command, args);

        var result = await hook.ExecuteAsync(AgentHookEvent.PreToolCall, new HookContext());

        result.Decision.Should().Be(HookDecision.Allow);
    }

    private static (string Command, string[] Args) CreatePowerShellCommand(string script)
    {
        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return (ResolvePowerShellExecutable(), ["-NoLogo", "-NoProfile", "-EncodedCommand", encodedCommand]);
    }

    private static string ResolvePowerShellExecutable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "pwsh";
        }

        var pwshPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerShell",
            "7",
            "pwsh.exe");

        return File.Exists(pwshPath) ? "pwsh" : "powershell";
    }
}
