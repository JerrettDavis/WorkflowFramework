using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Seeds the workflow definition store with pre-built sample workflows on startup.
/// </summary>
public static class SampleWorkflowSeeder
{
    public static async Task SeedAsync(IWorkflowDefinitionStore store, CancellationToken ct = default)
    {
        foreach (var workflow in CreateSamples())
        {
            await store.SeedAsync(workflow, ct);
        }
    }

    private static List<SavedWorkflowDefinition> CreateSamples()
    {
        var now = DateTimeOffset.UtcNow;
        return
        [
            // a) Hello World
            new SavedWorkflowDefinition
            {
                Id = "sample-hello-world",
                Description = "Simple two-step greeting workflow demonstrating basic action steps.",
                Tags = ["sample", "basic", "beginner"],
                LastModified = now,
                Definition = new WorkflowDefinitionDto
                {
                    Name = "Hello World",
                    Version = 1,
                    Steps =
                    [
                        Step("Greet", "Action", Cfg("expression", "Console.WriteLine('Hello from step!')")),
                        Step("Farewell", "Action", Cfg("expression", "Console.WriteLine('Goodbye!')"))
                    ]
                }
            },

            // b) Order Processing Pipeline
            new SavedWorkflowDefinition
            {
                Id = "sample-order-processing",
                Description = "Order validation and processing with conditional branching.",
                Tags = ["sample", "basic", "conditional"],
                LastModified = now,
                Definition = new WorkflowDefinitionDto
                {
                    Name = "Order Processing Pipeline",
                    Version = 1,
                    Steps =
                    [
                        Step("ValidateOrder", "Action", Cfg("expression", "Validate order total > 0")),
                        new StepDefinitionDto
                        {
                            Name = "CheckValidity", Type = "Conditional",
                            Config = Cfg("expression", "ctx.Data.IsValid"),
                            Then = Step("ProcessOrder", "Action", Cfg("expression", "Process the order")),
                            Else = Step("RejectOrder", "Action", Cfg("expression", "Reject invalid order"))
                        },
                        Step("Summary", "Action", Cfg("expression", "Print order summary"))
                    ]
                }
            },

            // c) Data Pipeline (ETL)
            new SavedWorkflowDefinition
            {
                Id = "sample-data-pipeline",
                Description = "ETL data pipeline: extract CSV records, filter, transform with markup, and load output.",
                Tags = ["sample", "data", "etl", "pipeline"],
                LastModified = now,
                Definition = new WorkflowDefinitionDto
                {
                    Name = "Data Pipeline (ETL)",
                    Version = 1,
                    Steps =
                    [
                        Step("Extract", "Action", Cfg("expression", "Parse CSV source data into records")),
                        Step("Transform", "Action", Cfg("expression", "Filter records (value > 10), uppercase names, apply 10% markup")),
                        Step("Load", "Action", Cfg("expression", "Generate output CSV from transformed records"))
                    ]
                }
            },

            // d) TaskStream — AI Task Extraction
            new SavedWorkflowDefinition
            {
                Id = "sample-taskstream",
                Description = "AI-powered task extraction from messages using agent loops with Ollama.",
                Tags = ["sample", "ai", "ollama", "taskstream", "agent"],
                LastModified = now,
                Definition = new WorkflowDefinitionDto
                {
                    Name = "TaskStream — AI Task Extraction",
                    Version = 1,
                    Steps =
                    [
                        Step("IngestMessages", "Action", Cfg("expression", "Load messages from configured sources")),
                        Step("ExtractTasks", "AgentLoopStep", new Dictionary<string, string>
                        {
                            ["provider"] = "ollama", ["model"] = "qwen3:30b-instruct",
                            ["systemPrompt"] = "Extract actionable tasks from the following messages. For each task, identify: title, category (work/personal/shopping/health), priority (1-4), and any deadlines.",
                            ["maxIterations"] = "5", ["tools"] = "create_todo,search_todos,update_todo,categorize"
                        }),
                        Step("TriageTasks", "Action", Cfg("expression", "Categorize and prioritize extracted tasks")),
                        Step("EnrichTasks", "AgentLoopStep", new Dictionary<string, string>
                        {
                            ["provider"] = "ollama", ["model"] = "qwen3:30b-instruct",
                            ["systemPrompt"] = "For each task, add context, estimate effort, suggest deadlines, and identify dependencies.",
                            ["maxIterations"] = "3", ["tools"] = "search_todos,update_todo,add_context"
                        }),
                        Step("GenerateReport", "Action", Cfg("expression", "Generate markdown summary report"))
                    ]
                }
            },

            // e) Quick Transcript
            new SavedWorkflowDefinition
            {
                Id = "sample-quick-transcript",
                Description = "Record audio, transcribe with Whisper, clean up with LLM, and review.",
                Tags = ["sample", "voice", "ollama", "transcript"],
                LastModified = now,
                Definition = new WorkflowDefinitionDto
                {
                    Name = "Quick Transcript",
                    Version = 1,
                    Steps =
                    [
                        Step("RecordAudio", "Action", Cfg("expression", "Record audio via microphone")),
                        Step("Transcribe", "Action", Cfg("expression", "Transcribe audio using Whisper")),
                        Step("LlmCleanup", "LlmCallStep", new Dictionary<string, string>
                        {
                            ["provider"] = "ollama", ["model"] = "qwen3:30b-instruct",
                            ["prompt"] = "Clean up and format the following raw transcript, fixing grammar and punctuation while preserving meaning:\n\n{rawTranscript}",
                            ["temperature"] = "0.3"
                        }),
                        Step("StoreCleanup", "Action", Cfg("expression", "Store cleaned transcript")),
                        Step("ReviewTranscript", "HumanTaskStep", new Dictionary<string, string>
                        {
                            ["assignee"] = "user", ["description"] = "Review the cleaned-up transcript for accuracy", ["priority"] = "Medium"
                        })
                    ]
                }
            },

            // f) Meeting Notes
            new SavedWorkflowDefinition
            {
                Id = "sample-meeting-notes",
                Description = "Transcribe meetings, format notes with LLM, extract action items, and review.",
                Tags = ["sample", "voice", "ollama", "meeting"],
                LastModified = now,
                Definition = new WorkflowDefinitionDto
                {
                    Name = "Meeting Notes",
                    Version = 1,
                    Steps =
                    [
                        Step("Transcribe", "Action", Cfg("expression", "Transcribe meeting recording")),
                        Step("CountSpeakers", "Action", Cfg("expression", "Detect number of speakers")),
                        Step("LabelSpeakers", "Action", Cfg("expression", "Label speakers in transcript")),
                        Step("FormatMeetingNotes", "LlmCallStep", new Dictionary<string, string>
                        {
                            ["provider"] = "ollama", ["model"] = "qwen3:30b-instruct",
                            ["prompt"] = "Format the following labeled transcript into professional meeting notes with sections for: Attendees, Key Discussion Points, Decisions Made, and Next Steps:\n\n{labeledTranscript}",
                            ["temperature"] = "0.3"
                        }),
                        Step("ExtractActionItems", "LlmCallStep", new Dictionary<string, string>
                        {
                            ["provider"] = "ollama", ["model"] = "qwen3:30b-instruct",
                            ["prompt"] = "Extract all action items from the following meeting notes. For each item include: assignee, description, deadline (if mentioned), and priority:\n\n{meetingNotes}",
                            ["temperature"] = "0.2"
                        }),
                        Step("StoreResults", "Action", Cfg("expression", "Store meeting notes and action items")),
                        Step("ReviewMeetingNotes", "HumanTaskStep", new Dictionary<string, string>
                        {
                            ["assignee"] = "user", ["description"] = "Review the formatted meeting notes and action items", ["priority"] = "Medium"
                        })
                    ]
                }
            },

            // g) Blog Interview
            new SavedWorkflowDefinition
            {
                Id = "sample-blog-interview",
                Description = "Voice-driven blog post creation: record topic, generate questions, synthesize blog.",
                Tags = ["sample", "voice", "ollama", "blog", "agent"],
                LastModified = now,
                Definition = new WorkflowDefinitionDto
                {
                    Name = "Blog Interview",
                    Version = 1,
                    Steps =
                    [
                        Step("RecordTopicIntro", "Action", Cfg("expression", "Record topic introduction audio")),
                        Step("TranscribeTopic", "Action", Cfg("expression", "Transcribe topic introduction")),
                        Step("CleanupTopic", "LlmCallStep", new Dictionary<string, string>
                        {
                            ["provider"] = "ollama", ["model"] = "qwen3:30b-instruct",
                            ["prompt"] = "Clean up this topic introduction transcript:\n\n{topicTranscript}", ["temperature"] = "0.3"
                        }),
                        Step("ReviewTopic", "HumanTaskStep", new Dictionary<string, string>
                        {
                            ["assignee"] = "user", ["description"] = "Review the topic introduction before generating questions"
                        }),
                        Step("GenerateQuestions", "AgentLoopStep", new Dictionary<string, string>
                        {
                            ["provider"] = "ollama", ["model"] = "qwen3:30b-instruct",
                            ["systemPrompt"] = "Based on the topic introduction, generate 5-7 thoughtful interview questions that would make an engaging blog post.",
                            ["maxIterations"] = "3", ["tools"] = "word_count,format_text"
                        }),
                        Step("ParseQuestions", "Action", Cfg("expression", "Parse numbered questions from agent output")),
                        Step("RecordAnswers", "Action", Cfg("expression", "Record and transcribe answers to each question")),
                        Step("SynthesizeBlog", "AgentLoopStep", new Dictionary<string, string>
                        {
                            ["provider"] = "ollama", ["model"] = "qwen3:30b-instruct",
                            ["systemPrompt"] = "Synthesize the following interview Q&A pairs into a compelling, well-structured blog post. Include an engaging introduction, organize by themes rather than Q&A format, and end with a strong conclusion.",
                            ["maxIterations"] = "3", ["tools"] = "word_count,format_text"
                        }),
                        Step("StoreBlogPost", "Action", Cfg("expression", "Store final blog post")),
                        Step("ReviewBlog", "HumanTaskStep", new Dictionary<string, string>
                        {
                            ["assignee"] = "user", ["description"] = "Review the final blog post before publishing", ["priority"] = "High"
                        })
                    ]
                }
            },

            // h) Brain Dump Synthesis
            new SavedWorkflowDefinition
            {
                Id = "sample-brain-dump",
                Description = "Record unstructured thoughts, transcribe, and synthesize into organized document.",
                Tags = ["sample", "voice", "ollama", "synthesis"],
                LastModified = now,
                Definition = new WorkflowDefinitionDto
                {
                    Name = "Brain Dump Synthesis",
                    Version = 1,
                    Steps =
                    [
                        Step("RecordBrainDump", "Action", Cfg("expression", "Record unstructured brain dump audio")),
                        Step("Transcribe", "Action", Cfg("expression", "Transcribe brain dump recording")),
                        Step("LlmCleanup", "LlmCallStep", new Dictionary<string, string>
                        {
                            ["provider"] = "ollama", ["model"] = "qwen3:30b-instruct",
                            ["prompt"] = "Clean up this raw brain dump transcript:\n\n{rawTranscript}", ["temperature"] = "0.3"
                        }),
                        Step("ReviewCleanup", "HumanTaskStep", new Dictionary<string, string>
                        {
                            ["assignee"] = "user", ["description"] = "Review the cleaned-up brain dump before synthesis"
                        }),
                        Step("Synthesize", "AgentLoopStep", new Dictionary<string, string>
                        {
                            ["provider"] = "ollama", ["model"] = "qwen3:30b-instruct",
                            ["systemPrompt"] = "Transform this cleaned-up brain dump into a well-organized document. Identify themes, group related ideas, create a logical structure with headers, and ensure NO ideas are lost — even tangential thoughts should be preserved in an appendix.",
                            ["maxIterations"] = "3", ["tools"] = "word_count,format_text,outline_generator"
                        }),
                        Step("StoreOutput", "Action", Cfg("expression", "Store structured document")),
                        Step("ReviewFinal", "HumanTaskStep", new Dictionary<string, string>
                        {
                            ["assignee"] = "user", ["description"] = "Review the final structured document", ["priority"] = "Medium"
                        })
                    ]
                }
            },

            // i) Podcast Transcript
            new SavedWorkflowDefinition
            {
                Id = "sample-podcast-transcript",
                Description = "Transcribe podcast, label speakers, summarize and format in parallel.",
                Tags = ["sample", "voice", "ollama", "podcast", "parallel"],
                LastModified = now,
                Definition = new WorkflowDefinitionDto
                {
                    Name = "Podcast Transcript",
                    Version = 1,
                    Steps =
                    [
                        Step("Transcribe", "Action", Cfg("expression", "Transcribe podcast episode")),
                        Step("LabelSpeakers", "Action", Cfg("expression", "Label speakers in podcast")),
                        new StepDefinitionDto
                        {
                            Name = "SummarizeAndFormat", Type = "Parallel",
                            Steps =
                            [
                                Step("Summarize", "LlmCallStep", new Dictionary<string, string>
                                {
                                    ["provider"] = "ollama", ["model"] = "qwen3:30b-instruct",
                                    ["prompt"] = "Create an executive summary of this podcast transcript, highlighting key topics discussed, notable quotes, and main takeaways:\n\n{labeledTranscript}",
                                    ["temperature"] = "0.4"
                                }),
                                Step("FormatTranscript", "LlmCallStep", new Dictionary<string, string>
                                {
                                    ["provider"] = "ollama", ["model"] = "qwen3:30b-instruct",
                                    ["prompt"] = "Format and clean up this podcast transcript for readability:\n\n{labeledTranscript}",
                                    ["temperature"] = "0.3"
                                })
                            ]
                        },
                        Step("MergeResults", "Action", Cfg("expression", "Merge summary and formatted transcript")),
                        Step("ReviewPodcast", "HumanTaskStep", new Dictionary<string, string>
                        {
                            ["assignee"] = "user", ["description"] = "Review the podcast summary and full transcript", ["priority"] = "Medium"
                        })
                    ]
                }
            },

            // j) HTTP API Orchestration
            new SavedWorkflowDefinition
            {
                Id = "sample-http-orchestration",
                Description = "Orchestrate multiple HTTP API calls: fetch data, transform, and post results.",
                Tags = ["sample", "http", "api", "integration"],
                LastModified = now,
                Definition = new WorkflowDefinitionDto
                {
                    Name = "HTTP API Orchestration",
                    Version = 1,
                    Steps =
                    [
                        Step("FetchUserData", "HttpStep", new Dictionary<string, string>
                        {
                            ["url"] = "https://jsonplaceholder.typicode.com/users/1", ["method"] = "GET", ["contentType"] = "application/json"
                        }),
                        Step("FetchUserPosts", "HttpStep", new Dictionary<string, string>
                        {
                            ["url"] = "https://jsonplaceholder.typicode.com/posts?userId=1", ["method"] = "GET"
                        }),
                        Step("TransformData", "Action", Cfg("expression", "Merge user profile with posts data")),
                        Step("SendNotification", "HttpStep", new Dictionary<string, string>
                        {
                            ["url"] = "https://httpbin.org/post", ["method"] = "POST",
                            ["body"] = "{\"message\": \"Data processed\", \"status\": \"complete\"}",
                            ["headers"] = "{\"Content-Type\": \"application/json\"}"
                        })
                    ]
                }
            },

            // k) Order Saga with Compensation
            new SavedWorkflowDefinition
            {
                Id = "sample-order-saga",
                Description = "Order processing with saga pattern and compensation (rollback on failure).",
                Tags = ["sample", "saga", "compensation", "order"],
                LastModified = now,
                Definition = new WorkflowDefinitionDto
                {
                    Name = "Order Saga with Compensation",
                    Version = 1,
                    Steps =
                    [
                        new StepDefinitionDto
                        {
                            Name = "OrderSaga", Type = "Saga",
                            Steps =
                            [
                                Step("ValidateOrder", "Action", Cfg("expression", "Validate order ID and items")),
                                Step("CheckInventory", "Action", Cfg("expression", "Reserve inventory for items (compensate: release reservation)")),
                                new StepDefinitionDto
                                {
                                    Name = "ShippingDecision", Type = "Conditional",
                                    Config = Cfg("expression", "ctx.Data.IsExpressShipping"),
                                    Then = Step("PrioritizeOrder", "Action", Cfg("expression", "Prioritize order for express shipping")),
                                    Else = Step("StandardProcessing", "Action", Cfg("expression", "Standard processing applied"))
                                },
                                Step("ChargePayment", "Action", Cfg("expression", "Charge payment (compensate: issue refund)")),
                                Step("SendConfirmation", "Action", Cfg("expression", "Send confirmation email"))
                            ]
                        }
                    ]
                }
            }
        ];
    }

    private static StepDefinitionDto Step(string name, string type, Dictionary<string, string>? config = null) =>
        new() { Name = name, Type = type, Config = config };

    private static Dictionary<string, string> Cfg(string key, string value) =>
        new() { [key] = value };
}
