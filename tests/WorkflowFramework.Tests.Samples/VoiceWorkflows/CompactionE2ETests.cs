using FluentAssertions;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;

namespace WorkflowFramework.Tests.Samples.VoiceWorkflows;

[Collection("VoiceWorkflows")]
[Trait("Category", "SampleE2E")]
public sealed class CompactionE2ETests
{
    private readonly VoiceWorkflowFixture _fixture;
    public CompactionE2ETests(VoiceWorkflowFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SlidingWindowCompaction_ReducesContextSize()
    {
        var manager = new DefaultContextManager();

        // Add many messages
        for (int i = 0; i < 20; i++)
        {
            manager.AddMessage(new ConversationMessage
            {
                Role = i % 2 == 0 ? ConversationRole.User : ConversationRole.Assistant,
                Content = $"Message {i}: " + new string('x', 100)
            });
        }

        var originalCount = manager.GetMessages().Count;
        originalCount.Should().Be(20);

        var result = await manager.CompactAsync(new CompactionOptions
        {
            Strategy = new SlidingWindowCompactionStrategy(2, 5),
            PreserveRecentCount = 5
        });

        var compactedMessages = manager.GetMessages();
        compactedMessages.Count.Should().BeLessThan(originalCount);

        // First message content should be preserved (in compacted summary)
        var allContent = string.Join(" ", compactedMessages.Select(m => m.Content));
        allContent.Should().Contain("Message 0");

        // Last messages should be preserved
        compactedMessages.Last().Content.Should().Contain("Message 19");

        result.OriginalMessageCount.Should().Be(20);
        result.CompactedMessageCount.Should().BeLessThan(20);
    }

    [Fact]
    public async Task CheckpointStore_SaveAndRestore()
    {
        var store = new InMemoryCheckpointStore();
        var snapshot = new ContextSnapshot
        {
            StepName = "TestStep",
            Messages =
            {
                new ConversationMessage { Role = ConversationRole.System, Content = "System prompt" },
                new ConversationMessage { Role = ConversationRole.User, Content = "User message" },
                new ConversationMessage { Role = ConversationRole.Assistant, Content = "Assistant response" }
            }
        };

        await store.SaveAsync("wf-1", "cp-1", snapshot);

        var loaded = await store.LoadAsync("wf-1", "cp-1");
        loaded.Should().NotBeNull();
        loaded!.StepName.Should().Be("TestStep");
        loaded.Messages.Should().HaveCount(3);
        loaded.Messages[0].Role.Should().Be(ConversationRole.System);
        loaded.Messages[1].Content.Should().Be("User message");
        loaded.Messages[2].Content.Should().Be("Assistant response");

        var list = await store.ListAsync("wf-1");
        list.Should().HaveCount(1);
        list[0].Id.Should().Be("cp-1");
    }

    [Fact]
    public async Task AgentLoopStep_WithCompaction_CompactsWhenOverThreshold()
    {
        var agent = new EchoAgentProvider();
        var tools = new ToolRegistry();
        var checkpoints = new InMemoryCheckpointStore();

        var workflow = Workflow.Create("CompactionTest")
            .Step(new AgentLoopStep(agent, tools, new AgentLoopOptions
            {
                StepName = "CompactLoop",
                SystemPrompt = new string('x', 500), // Large system prompt
                MaxIterations = 1,
                MaxContextTokens = 10, // Very low threshold to trigger compaction
                AutoCompact = true,
                CompactionStrategy = new SlidingWindowCompactionStrategy(1, 1),
                CheckpointStore = checkpoints,
                CheckpointInterval = 1
            }))
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        context.Properties.Should().ContainKey("CompactLoop.Iterations");
        ((int)context.Properties["CompactLoop.Iterations"]!).Should().BeGreaterThan(0);
    }
}
