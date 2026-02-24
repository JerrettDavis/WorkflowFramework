using FluentAssertions;
using WorkflowFramework.Triggers;
using WorkflowFramework.Triggers.Sources;
using Xunit;

namespace WorkflowFramework.Tests.Triggers;

public class AudioInputTriggerSourceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "wf_audiotest_" + Guid.NewGuid().ToString("N")[..8]);

    public AudioInputTriggerSourceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private TriggerDefinition MakeDef(string? formats = null) => new()
    {
        Type = "audio",
        Configuration = new Dictionary<string, string>
        {
            ["watchPath"] = _tempDir,
            ["formats"] = formats ?? "wav,mp3"
        }
    };

    [Fact]
    public void Type_IsAudio()
    {
        new AudioInputTriggerSource(MakeDef()).Type.Should().Be("audio");
    }

    [Fact]
    public async Task StartAsync_SetsIsRunning()
    {
        var source = new AudioInputTriggerSource(MakeDef());
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = MakeDef().Configuration,
            OnTriggered = _ => Task.FromResult("run1")
        };
        await source.StartAsync(ctx);
        source.IsRunning.Should().BeTrue();
        await source.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_MissingWatchPath_Throws()
    {
        var source = new AudioInputTriggerSource(new TriggerDefinition { Type = "audio" });
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = new Dictionary<string, string>(),
            OnTriggered = _ => Task.FromResult("run1")
        };
        var act = () => source.StartAsync(ctx);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AudioFile_FiresTrigger()
    {
        var tcs = new TaskCompletionSource<TriggerEvent>();
        var source = new AudioInputTriggerSource(MakeDef());
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = MakeDef().Configuration,
            OnTriggered = e => { tcs.TrySetResult(e); return Task.FromResult("run1"); }
        };
        await source.StartAsync(ctx);

        await File.WriteAllBytesAsync(Path.Combine(_tempDir, "test.wav"), new byte[] { 0, 1, 2 });

        var evt = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        if (evt == tcs.Task)
        {
            var triggerEvt = await tcs.Task;
            triggerEvt.TriggerType.Should().Be("audio");
            triggerEvt.Payload.Should().ContainKey("format");
            triggerEvt.Payload["format"].Should().Be("wav");
        }

        await source.DisposeAsync();
    }

    [Fact]
    public async Task NonAudioFile_DoesNotFire()
    {
        var fired = false;
        var source = new AudioInputTriggerSource(MakeDef("wav"));
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = new Dictionary<string, string> { ["watchPath"] = _tempDir, ["formats"] = "wav" },
            OnTriggered = _ => { fired = true; return Task.FromResult("run1"); }
        };
        await source.StartAsync(ctx);

        await File.WriteAllTextAsync(Path.Combine(_tempDir, "test.txt"), "hello");
        await Task.Delay(1500);
        fired.Should().BeFalse();

        await source.DisposeAsync();
    }

    [Fact]
    public void Constructor_NullDefinition_Throws()
    {
        var act = () => new AudioInputTriggerSource(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task StopAsync_ClearsIsRunning()
    {
        var source = new AudioInputTriggerSource(MakeDef());
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = MakeDef().Configuration,
            OnTriggered = _ => Task.FromResult("run1")
        };
        await source.StartAsync(ctx);
        await source.StopAsync();
        source.IsRunning.Should().BeFalse();
        await source.DisposeAsync();
    }
}
