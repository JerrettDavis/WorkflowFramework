using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Extensions.HumanTasks;
using WorkflowFramework.Samples.VoiceWorkflows.Hooks;

namespace WorkflowFramework.Samples.VoiceWorkflows.Workflows;

/// <summary>Static factory with all preset voice workflow builders.</summary>
public static class VoiceWorkflowPresets
{
    /// <summary>
    /// Quick transcript: Record → Transcribe → LLM Cleanup → Human Review.
    /// </summary>
    public static IWorkflow QuickTranscript(
        IAgentProvider agent,
        ToolRegistry tools,
        ITaskInbox inbox,
        HookPipeline hooks,
        ICheckpointStore checkpoints)
    {
        return Workflow.Create("QuickTranscript")
            .Step("RecordAudio", async ctx =>
            {
                var result = await tools.InvokeAsync("record_audio", "{}", ctx.CancellationToken);
                ctx.Properties["audioPath"] = result.Content;
                Console.WriteLine($"  🎤 Recorded audio: {result.Content}");
            })
            .Step("Transcribe", async ctx =>
            {
                var result = await tools.InvokeAsync("transcribe",
                    """{"audio_path":"recording.wav"}""", ctx.CancellationToken);
                ctx.Properties["rawTranscript"] = result.Content;
                Console.WriteLine($"  📝 Transcribed ({result.Content.Length} chars)");
            })
            .Step(new LlmCallStep(agent, new LlmCallOptions
            {
                StepName = "LlmCleanup",
                PromptTemplate = PromptTemplates.CleanupPrompt
            }))
            .Step("StoreCleanup", ctx =>
            {
                ctx.Properties["processedText"] = ctx.Properties["LlmCleanup.Response"];
                Console.WriteLine("  ✨ Text cleaned up by LLM");
                return Task.CompletedTask;
            })
            .Step(new HumanTaskStep(inbox, new HumanTaskOptions
            {
                StepName = "ReviewTranscript",
                Title = "Review Transcript",
                Description = "Review the cleaned-up transcript for accuracy",
                Timeout = TimeSpan.FromSeconds(5)
            }))
            .Build();
    }

    /// <summary>
    /// Meeting notes: Transcribe → Speaker Labels → LLM Format → Extract Action Items → Human Review.
    /// </summary>
    public static IWorkflow MeetingNotes(
        IAgentProvider agent,
        ToolRegistry tools,
        ITaskInbox inbox,
        HookPipeline hooks,
        ICheckpointStore checkpoints)
    {
        return Workflow.Create("MeetingNotes")
            .Step("Transcribe", async ctx =>
            {
                var result = await tools.InvokeAsync("transcribe",
                    """{"audio_path":"meeting_recording.wav"}""", ctx.CancellationToken);
                ctx.Properties["rawTranscript"] = result.Content;
                Console.WriteLine($"  📝 Transcribed meeting ({result.Content.Length} chars)");
            })
            .Step("CountSpeakers", async ctx =>
            {
                var result = await tools.InvokeAsync("count_speakers",
                    """{"audio_path":"meeting_recording.wav"}""", ctx.CancellationToken);
                ctx.Properties["speakerInfo"] = result.Content;
                Console.WriteLine($"  👥 Speaker count: {result.Content}");
            })
            .Step("LabelSpeakers", async ctx =>
            {
                var transcript = (string)ctx.Properties["rawTranscript"]!;
                var result = await tools.InvokeAsync("label_speakers",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        transcript,
                        audio_path = "meeting_recording.wav",
                        speaker_count = 2
                    }), ctx.CancellationToken);
                ctx.Properties["labeledTranscript"] = result.Content;
                Console.WriteLine($"  🏷️ Speakers labeled ({result.Content.Length} chars)");
            })
            .Step(new LlmCallStep(agent, new LlmCallOptions
            {
                StepName = "FormatMeetingNotes",
                PromptTemplate = PromptTemplates.MeetingNotesPrompt
            }))
            .Step(new LlmCallStep(agent, new LlmCallOptions
            {
                StepName = "ExtractActionItems",
                PromptTemplate = PromptTemplates.ActionItemsPrompt
            }))
            .Step("StoreResults", ctx =>
            {
                ctx.Properties["meetingNotes"] = ctx.Properties["FormatMeetingNotes.Response"];
                ctx.Properties["actionItems"] = ctx.Properties["ExtractActionItems.Response"];
                Console.WriteLine("  📋 Meeting notes and action items extracted");
                return Task.CompletedTask;
            })
            .Step(new HumanTaskStep(inbox, new HumanTaskOptions
            {
                StepName = "ReviewMeetingNotes",
                Title = "Review Meeting Notes",
                Description = "Review the formatted meeting notes and action items",
                Timeout = TimeSpan.FromSeconds(5)
            }))
            .Build();
    }

    /// <summary>
    /// Blog interview: multi-phase with agent loop, compaction, checkpointing.
    /// </summary>
    public static IWorkflow BlogInterview(
        IAgentProvider agent,
        ToolRegistry tools,
        ITaskInbox inbox,
        HookPipeline hooks,
        ICheckpointStore checkpoints)
    {
        return Workflow.Create("BlogInterview")
            // Phase 1: Record topic intro
            .Step("RecordTopicIntro", async ctx =>
            {
                var result = await tools.InvokeAsync("record_audio", "{}", ctx.CancellationToken);
                ctx.Properties["topicAudioPath"] = result.Content;
                Console.WriteLine("  🎤 Phase 1: Recorded topic introduction");
            })
            .Step("TranscribeTopic", async ctx =>
            {
                var result = await tools.InvokeAsync("transcribe",
                    """{"audio_path":"topic_intro.wav"}""", ctx.CancellationToken);
                ctx.Properties["topicTranscript"] = result.Content;
                Console.WriteLine("  📝 Transcribed topic intro");
            })
            .Step(new LlmCallStep(agent, new LlmCallOptions
            {
                StepName = "CleanupTopic",
                PromptTemplate = PromptTemplates.CleanupPrompt
            }))
            .Step(new HumanTaskStep(inbox, new HumanTaskOptions
            {
                StepName = "ReviewTopic",
                Title = "Review Topic Introduction",
                Description = "Review the topic introduction before generating questions",
                Timeout = TimeSpan.FromSeconds(5)
            }))
            // Phase 2: Generate questions via agent loop
            .Step(new AgentLoopStep(agent, tools, new AgentLoopOptions
            {
                StepName = "GenerateQuestions",
                SystemPrompt = PromptTemplates.QuestionGenerationPrompt,
                MaxIterations = 3,
                Hooks = hooks,
                CheckpointStore = checkpoints,
                CheckpointInterval = 1,
                MaxContextTokens = 50000,
                AutoCompact = true,
                CompactionStrategy = new SlidingWindowCompactionStrategy(2, 5)
            }))
            .Step("ParseQuestions", ctx =>
            {
                var response = (string?)ctx.Properties["GenerateQuestions.Response"] ?? "";
                // Extract numbered questions
                var questions = response.Split('\n')
                    .Where(l => l.Trim().Length > 0 && char.IsDigit(l.Trim()[0]))
                    .Select(l => l.Trim().TrimStart("0123456789.) ".ToCharArray()))
                    .Where(q => q.Length > 10)
                    .ToList();
                if (questions.Count == 0)
                    questions = ["What inspired this project?", "What were the biggest challenges?",
                        "What would you do differently?", "What advice would you give others?",
                        "What's next for the project?"];
                ctx.Properties["questions"] = questions;
                Console.WriteLine($"  ❓ Phase 2: Generated {questions.Count} interview questions");
                return Task.CompletedTask;
            })
            // Phase 3: Record and transcribe answers
            .Step("RecordAnswers", async ctx =>
            {
                var questions = (List<string>)ctx.Properties["questions"]!;
                var qaPairs = new List<(string q, string a)>();
                foreach (var question in questions)
                {
                    Console.WriteLine($"  ❓ Q: {question}");
                    await tools.InvokeAsync("record_audio", "{}", ctx.CancellationToken);
                    var transcribeResult = await tools.InvokeAsync("transcribe",
                        """{"audio_path":"answer.wav"}""", ctx.CancellationToken);
                    qaPairs.Add((question, transcribeResult.Content));
                    Console.WriteLine($"  💬 A: ({transcribeResult.Content.Length} chars)");
                }
                ctx.Properties["qaPairs"] = qaPairs;
                var qaContent = string.Join("\n\n", qaPairs.Select(p => $"Q: {p.q}\nA: {p.a}"));
                ctx.Properties["qaContent"] = qaContent;
                Console.WriteLine($"  📝 Phase 3: Recorded {qaPairs.Count} Q&A pairs");
            })
            // Phase 4: Synthesize blog post via agent loop
            .Step(new AgentLoopStep(agent, tools, new AgentLoopOptions
            {
                StepName = "SynthesizeBlog",
                SystemPrompt = PromptTemplates.BlogPostPrompt,
                MaxIterations = 3,
                Hooks = hooks,
                CheckpointStore = checkpoints,
                CheckpointInterval = 1,
                MaxContextTokens = 50000,
                AutoCompact = true,
                CompactionStrategy = new SlidingWindowCompactionStrategy(2, 5),
                CompactionFocusInstructions = "Focus on preserving the key insights and quotes from the interview."
            }))
            .Step("StoreBlogPost", ctx =>
            {
                ctx.Properties["finalOutput"] = ctx.Properties["SynthesizeBlog.Response"];
                Console.WriteLine("  📰 Phase 4: Blog post synthesized");
                return Task.CompletedTask;
            })
            // Phase 5: Final review
            .Step(new HumanTaskStep(inbox, new HumanTaskOptions
            {
                StepName = "ReviewBlog",
                Title = "Review Blog Post",
                Description = "Review the final blog post before publishing",
                Timeout = TimeSpan.FromSeconds(5)
            }))
            .Build();
    }

    /// <summary>
    /// Brain dump synthesis: unstructured recording → structured document.
    /// </summary>
    public static IWorkflow BrainDumpSynthesis(
        IAgentProvider agent,
        ToolRegistry tools,
        ITaskInbox inbox,
        HookPipeline hooks,
        ICheckpointStore checkpoints)
    {
        return Workflow.Create("BrainDumpSynthesis")
            .Step("RecordBrainDump", async ctx =>
            {
                var result = await tools.InvokeAsync("record_audio", "{}", ctx.CancellationToken);
                ctx.Properties["audioPath"] = result.Content;
                Console.WriteLine("  🎤 Recorded brain dump");
            })
            .Step("Transcribe", async ctx =>
            {
                var result = await tools.InvokeAsync("transcribe",
                    """{"audio_path":"brain_dump.wav"}""", ctx.CancellationToken);
                ctx.Properties["rawTranscript"] = result.Content;
                Console.WriteLine($"  📝 Transcribed ({result.Content.Length} chars)");
            })
            .Step(new LlmCallStep(agent, new LlmCallOptions
            {
                StepName = "LlmCleanup",
                PromptTemplate = PromptTemplates.CleanupPrompt
            }))
            .Step(new HumanTaskStep(inbox, new HumanTaskOptions
            {
                StepName = "ReviewCleanup",
                Title = "Review Cleaned Transcript",
                Description = "Review the cleaned-up brain dump before synthesis",
                Timeout = TimeSpan.FromSeconds(5)
            }))
            .Step(new AgentLoopStep(agent, tools, new AgentLoopOptions
            {
                StepName = "Synthesize",
                SystemPrompt = PromptTemplates.SynthesisPrompt,
                MaxIterations = 3,
                Hooks = hooks,
                CheckpointStore = checkpoints,
                CheckpointInterval = 1,
                MaxContextTokens = 50000,
                AutoCompact = true,
                CompactionStrategy = new SlidingWindowCompactionStrategy(2, 5),
                CompactionFocusInstructions = "Preserve ALL ideas and tangential thoughts. Nothing should be lost."
            }))
            .Step("StoreOutput", ctx =>
            {
                ctx.Properties["finalOutput"] = ctx.Properties["Synthesize.Response"];
                Console.WriteLine("  📄 Brain dump synthesized into structured document");
                return Task.CompletedTask;
            })
            .Step(new HumanTaskStep(inbox, new HumanTaskOptions
            {
                StepName = "ReviewFinal",
                Title = "Review Structured Document",
                Description = "Review the final structured document",
                Timeout = TimeSpan.FromSeconds(5)
            }))
            .Build();
    }

    /// <summary>
    /// Podcast transcript: parallel branches for summary + full transcript, then merge.
    /// </summary>
    public static IWorkflow PodcastTranscript(
        IAgentProvider agent,
        ToolRegistry tools,
        ITaskInbox inbox,
        HookPipeline hooks,
        ICheckpointStore checkpoints)
    {
        return Workflow.Create("PodcastTranscript")
            .Step("Transcribe", async ctx =>
            {
                var result = await tools.InvokeAsync("transcribe",
                    """{"audio_path":"podcast_episode.wav"}""", ctx.CancellationToken);
                ctx.Properties["rawTranscript"] = result.Content;
                Console.WriteLine($"  📝 Transcribed podcast ({result.Content.Length} chars)");
            })
            .Step("LabelSpeakers", async ctx =>
            {
                var transcript = (string)ctx.Properties["rawTranscript"]!;
                var result = await tools.InvokeAsync("label_speakers",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        transcript,
                        audio_path = "podcast_episode.wav",
                        speaker_count = 2
                    }), ctx.CancellationToken);
                ctx.Properties["labeledTranscript"] = result.Content;
                Console.WriteLine("  🏷️ Speakers labeled");
            })
            // Parallel: Summarize + Format full transcript
            .Parallel(p =>
            {
                p.Step(new LlmCallStep(agent, new LlmCallOptions
                {
                    StepName = "Summarize",
                    PromptTemplate = PromptTemplates.SummarizePrompt
                }));
                p.Step(new LlmCallStep(agent, new LlmCallOptions
                {
                    StepName = "FormatTranscript",
                    PromptTemplate = PromptTemplates.CleanupPrompt
                }));
            })
            // Merge results
            .Step("MergeResults", ctx =>
            {
                var summary = (string?)ctx.Properties["Summarize.Response"] ?? "(no summary)";
                var transcript = (string?)ctx.Properties["FormatTranscript.Response"] ?? "(no transcript)";
                var merged = $"""
                    # Podcast Episode

                    ## Executive Summary
                    {summary}

                    ---

                    ## Full Transcript
                    {transcript}
                    """;
                ctx.Properties["finalOutput"] = merged;
                Console.WriteLine("  🔀 Merged summary + transcript");
                return Task.CompletedTask;
            })
            .Step(new HumanTaskStep(inbox, new HumanTaskOptions
            {
                StepName = "ReviewPodcast",
                Title = "Review Podcast Transcript",
                Description = "Review the podcast summary and full transcript",
                Timeout = TimeSpan.FromSeconds(5)
            }))
            .Build();
    }
}
