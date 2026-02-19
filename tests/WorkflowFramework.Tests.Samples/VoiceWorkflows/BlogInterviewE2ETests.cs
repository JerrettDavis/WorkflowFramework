using FluentAssertions;

namespace WorkflowFramework.Tests.Samples.VoiceWorkflows;

[Collection("VoiceWorkflows")]
[Trait("Category", "SampleE2E")]
public sealed class BlogInterviewE2ETests
{
    private readonly VoiceWorkflowFixture _fixture;
    public BlogInterviewE2ETests(VoiceWorkflowFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task BlogInterview_RunsToCompletion()
    {
        var workflow = _fixture.CreateBlogInterview();
        var context = new WorkflowContext();

        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        context.Errors.Should().BeEmpty();

        context.Properties.Should().ContainKey("topicTranscript");
        ((string)context.Properties["topicTranscript"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("questions");
        var questions = context.Properties["questions"] as List<string>;
        questions.Should().NotBeNull();
        questions!.Should().HaveCountGreaterThan(0);

        context.Properties.Should().ContainKey("qaPairs");
        var qaPairs = context.Properties["qaPairs"] as List<(string q, string a)>;
        qaPairs.Should().NotBeNull();
        qaPairs!.Should().HaveCountGreaterThan(0);

        context.Properties.Should().ContainKey("GenerateQuestions.Response");
        ((string)context.Properties["GenerateQuestions.Response"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("SynthesizeBlog.Response");
        ((string)context.Properties["SynthesizeBlog.Response"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("finalOutput");
        ((string)context.Properties["finalOutput"]!).Should().NotBeNullOrEmpty();

        context.Properties.Should().ContainKey("GenerateQuestions.Iterations");
        ((int)context.Properties["GenerateQuestions.Iterations"]!).Should().BeGreaterThan(0);

        context.Properties.Should().ContainKey("SynthesizeBlog.Iterations");
        ((int)context.Properties["SynthesizeBlog.Iterations"]!).Should().BeGreaterThan(0);
    }
}
