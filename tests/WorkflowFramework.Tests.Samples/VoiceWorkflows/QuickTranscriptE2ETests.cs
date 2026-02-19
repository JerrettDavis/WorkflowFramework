using FluentAssertions;

namespace WorkflowFramework.Tests.Samples.VoiceWorkflows;

[Collection("VoiceWorkflows")]
[Trait("Category", "SampleE2E")]
public sealed class QuickTranscriptE2ETests
{
    private readonly VoiceWorkflowFixture _fixture;
    public QuickTranscriptE2ETests(VoiceWorkflowFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task QuickTranscript_RunsToCompletion()
    {
        var workflow = _fixture.CreateQuickTranscript();
        var context = new WorkflowContext();

        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        context.Errors.Should().BeEmpty();
        context.Properties.Should().ContainKey("rawTranscript");
        ((string)context.Properties["rawTranscript"]!).Should().NotBeNullOrEmpty();
        context.Properties.Should().ContainKey("processedText");
        ((string)context.Properties["processedText"]!).Should().NotBeNullOrEmpty();
        context.Properties.Should().ContainKey("LlmCleanup.Response");
        ((string)context.Properties["LlmCleanup.Response"]!).Should().NotBeNullOrEmpty();
    }
}
