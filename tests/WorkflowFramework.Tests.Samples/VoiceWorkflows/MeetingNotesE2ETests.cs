using FluentAssertions;

namespace WorkflowFramework.Tests.Samples.VoiceWorkflows;

[Collection("VoiceWorkflows")]
[Trait("Category", "SampleE2E")]
public sealed class MeetingNotesE2ETests
{
    private readonly VoiceWorkflowFixture _fixture;
    public MeetingNotesE2ETests(VoiceWorkflowFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task MeetingNotes_RunsToCompletion()
    {
        var workflow = _fixture.CreateMeetingNotes();
        var context = new WorkflowContext();

        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        context.Errors.Should().BeEmpty();

        context.Properties.Should().ContainKey("rawTranscript");
        ((string)context.Properties["rawTranscript"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("labeledTranscript");
        ((string)context.Properties["labeledTranscript"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("speakerInfo");
        ((string)context.Properties["speakerInfo"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("FormatMeetingNotes.Response");
        ((string)context.Properties["FormatMeetingNotes.Response"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("ExtractActionItems.Response");
        ((string)context.Properties["ExtractActionItems.Response"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("meetingNotes");
        ((string)context.Properties["meetingNotes"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("actionItems");
        ((string)context.Properties["actionItems"]!).Should().NotBeNullOrEmpty();
    }
}
