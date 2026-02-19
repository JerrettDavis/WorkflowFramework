using FluentAssertions;

namespace WorkflowFramework.Tests.Samples.VoiceWorkflows;

[Collection("VoiceWorkflows")]
[Trait("Category", "SampleE2E")]
public sealed class BrainDumpSynthesisE2ETests
{
    private readonly VoiceWorkflowFixture _fixture;
    public BrainDumpSynthesisE2ETests(VoiceWorkflowFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task BrainDumpSynthesis_RunsToCompletion()
    {
        var workflow = _fixture.CreateBrainDumpSynthesis();
        var context = new WorkflowContext();

        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        context.Errors.Should().BeEmpty();

        context.Properties.Should().ContainKey("rawTranscript");
        ((string)context.Properties["rawTranscript"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("Synthesize.Response");
        ((string)context.Properties["Synthesize.Response"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("finalOutput");
        ((string)context.Properties["finalOutput"]!).Should().NotBeNullOrEmpty();
    }
}
