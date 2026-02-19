using System.Text.Json;
using FluentAssertions;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Samples.VoiceWorkflows.Models;
using WorkflowFramework.Samples.VoiceWorkflows.Tools;

namespace WorkflowFramework.Tests.Samples.VoiceWorkflows;

[Collection("VoiceWorkflows")]
[Trait("Category", "SampleE2E")]
public sealed class ToolProviderE2ETests
{
    private readonly VoiceWorkflowFixture _fixture;
    public ToolProviderE2ETests(VoiceWorkflowFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task WhisperToolProvider_Transcribe_ReturnsRealisticTranscript()
    {
        var result = await _fixture.Tools.InvokeAsync("transcribe",
            """{"audio_path":"test.wav"}""", CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Content.Should().NotBeNullOrEmpty();
        result.Content.Should().Contain("architecture");
    }

    [Fact]
    public async Task WhisperToolProvider_DetectLanguage_ReturnsLanguage()
    {
        var result = await _fixture.Tools.InvokeAsync("detect_language",
            """{"audio_path":"test.wav"}""", CancellationToken.None);

        result.IsError.Should().BeFalse();
        var json = JsonSerializer.Deserialize<JsonElement>(result.Content);
        json.GetProperty("language").GetString().Should().Be("en");
    }

    [Fact]
    public async Task SpeakerDiarizationToolProvider_LabelSpeakers_ReturnsSpeakerLabels()
    {
        var result = await _fixture.Tools.InvokeAsync("label_speakers",
            """{"transcript":"hello world","audio_path":"test.wav","speaker_count":2}""",
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Content.Should().Contain("Speaker 1");
        result.Content.Should().Contain("Speaker 2");
    }

    [Fact]
    public async Task SpeakerDiarizationToolProvider_CountSpeakers_ReturnsCount()
    {
        var result = await _fixture.Tools.InvokeAsync("count_speakers",
            """{"audio_path":"test.wav"}""", CancellationToken.None);

        result.IsError.Should().BeFalse();
        var json = JsonSerializer.Deserialize<JsonElement>(result.Content);
        json.GetProperty("speaker_count").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task AudioToolProvider_RecordAudio_ReturnsPath()
    {
        var result = await _fixture.Tools.InvokeAsync("record_audio", "{}", CancellationToken.None);

        result.IsError.Should().BeFalse();
        var json = JsonSerializer.Deserialize<JsonElement>(result.Content);
        json.GetProperty("file_path").GetString().Should().Contain("recordings/");
    }

    [Fact]
    public async Task AudioToolProvider_NormalizeAudio_ReturnsPath()
    {
        var result = await _fixture.Tools.InvokeAsync("normalize_audio",
            """{"audio_path":"test.wav"}""", CancellationToken.None);

        result.IsError.Should().BeFalse();
        var json = JsonSerializer.Deserialize<JsonElement>(result.Content);
        json.GetProperty("normalized_path").GetString().Should().Contain("normalized");
    }

    [Fact]
    public async Task TextToolProvider_ChunkText_SplitsCorrectly()
    {
        var result = await _fixture.Tools.InvokeAsync("chunk_text",
            """{"text":"abcdefghij","max_chars":4}""", CancellationToken.None);

        result.IsError.Should().BeFalse();
        var json = JsonSerializer.Deserialize<JsonElement>(result.Content);
        json.GetProperty("count").GetInt32().Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task TextToolProvider_MergeTexts_CombinesWithSeparator()
    {
        var result = await _fixture.Tools.InvokeAsync("merge_texts",
            """{"texts":["hello","world"],"separator":" | "}""", CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Content.Should().Be("hello | world");
    }

    [Fact]
    public async Task TextToolProvider_RegexReplace_AppliesPattern()
    {
        var result = await _fixture.Tools.InvokeAsync("regex_replace",
            """{"text":"hello 123 world","pattern":"\\d+","replacement":"NUM"}""",
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Content.Should().Be("hello NUM world");
    }

    [Fact]
    public async Task TextToolProvider_ExtractJson_ExtractsField()
    {
        var result = await _fixture.Tools.InvokeAsync("extract_json",
            """{"text":"prefix {\"name\":\"test\",\"value\":42} suffix","path":"name"}""",
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Content.Should().Be("test");
    }

    [Fact]
    public async Task ToolRegistry_ListAllTools_ReturnsAllRegisteredTools()
    {
        var tools = await _fixture.Tools.ListAllToolsAsync(CancellationToken.None);

        tools.Should().HaveCount(10);
        var names = tools.Select(t => t.Name).ToList();
        names.Should().Contain("transcribe");
        names.Should().Contain("detect_language");
        names.Should().Contain("label_speakers");
        names.Should().Contain("count_speakers");
        names.Should().Contain("record_audio");
        names.Should().Contain("normalize_audio");
        names.Should().Contain("chunk_text");
        names.Should().Contain("merge_texts");
        names.Should().Contain("regex_replace");
        names.Should().Contain("extract_json");
    }
}
