using FluentAssertions;

namespace WorkflowFramework.Tests.Samples.VoiceWorkflows;

[Collection("VoiceWorkflows")]
[Trait("Category", "SampleE2E")]
public sealed class PodcastTranscriptE2ETests
{
    private readonly VoiceWorkflowFixture _fixture;
    public PodcastTranscriptE2ETests(VoiceWorkflowFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PodcastTranscript_RunsToCompletion()
    {
        var workflow = _fixture.CreatePodcastTranscript();
        var context = new WorkflowContext();

        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        context.Errors.Should().BeEmpty();

        context.Properties.Should().ContainKey("rawTranscript");
        ((string)context.Properties["rawTranscript"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("labeledTranscript");
        ((string)context.Properties["labeledTranscript"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("Summarize.Response");
        ((string)context.Properties["Summarize.Response"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("FormatTranscript.Response");
        ((string)context.Properties["FormatTranscript.Response"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("finalOutput");
        var finalOutput = (string)context.Properties["finalOutput"]!;
        finalOutput.Should().Contain("Executive Summary");
        finalOutput.Should().Contain("Full Transcript");
    }
}
