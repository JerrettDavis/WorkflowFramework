using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// In-memory template library pre-loaded with all workflow templates.
/// </summary>
public sealed class InMemoryWorkflowTemplateLibrary : IWorkflowTemplateLibrary
{
    private readonly List<WorkflowTemplate> _templates;

    public InMemoryWorkflowTemplateLibrary()
    {
        _templates = BuildAllTemplates();
    }

    public Task<IReadOnlyList<WorkflowTemplateSummary>> GetTemplatesAsync(string? category = null, string? tag = null, CancellationToken ct = default)
    {
        var query = _templates.AsEnumerable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(tag))
            query = query.Where(t => t.Tags.Any(tg => string.Equals(tg, tag, StringComparison.OrdinalIgnoreCase)));

        var result = query.Select(t => new WorkflowTemplateSummary
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            Category = t.Category,
            Tags = t.Tags,
            Difficulty = t.Difficulty,
            StepCount = t.StepCount,
            PreviewImageUrl = t.PreviewImageUrl
        }).ToList();

        return Task.FromResult<IReadOnlyList<WorkflowTemplateSummary>>(result);
    }

    public Task<WorkflowTemplate?> GetTemplateAsync(string id, CancellationToken ct = default)
    {
        var template = _templates.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(template);
    }

    public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var categories = _templates.Select(t => t.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();
        return Task.FromResult<IReadOnlyList<string>>(categories);
    }

    private static List<WorkflowTemplate> BuildAllTemplates()
    {
        var templates = new List<WorkflowTemplate>();

        // === Getting Started ===
        templates.Add(HelloWorld());
        templates.Add(SequentialPipeline());
        templates.Add(ConditionalBranching());
        templates.Add(ParallelExecution());
        templates.Add(ErrorHandling());
        templates.Add(RetryWithBackoff());
        templates.Add(LoopProcessing());

        // === Data Processing ===
        templates.Add(CsvEtlPipeline());
        templates.Add(DataMappingTransform());
        templates.Add(SchemaValidation());

        // === Order Management ===
        templates.Add(OrderProcessingSaga());
        templates.Add(ExpressOrderFlow());
        templates.Add(OrderWithApproval());

        // === AI & Agents ===
        templates.Add(TaskExtractionPipeline());
        templates.Add(AgentTriageWorkflow());

        // === Voice & Audio ===
        templates.Add(QuickTranscript());
        templates.Add(MeetingNotes());
        templates.Add(BlogFromInterview());
        templates.Add(BrainDumpSynthesis());
        templates.Add(PodcastTranscript());

        // === Integration Patterns ===
        templates.Add(ContentBasedRouter());
        templates.Add(ScatterGather());
        templates.Add(PublishSubscribe());
        templates.Add(HttpApiOrchestration());
        templates.Add(WebhookHandler());

        return templates;
    }

    // ── Getting Started ─────────────────────────────────────────

    private static WorkflowTemplate HelloWorld() => new()
    {
        Id = "hello-world",
        Name = "Hello World",
        Description = "The simplest possible workflow — a single action step that greets the user.",
        Category = "Getting Started",
        Tags = ["beginner", "simple", "action"],
        Difficulty = TemplateDifficulty.Beginner,
        StepCount = 1,
        Definition = new WorkflowDefinitionDto
        {
            Name = "HelloWorkflow",
            Steps = [new StepDefinitionDto { Name = "Greet", Type = "Action" }]
        }
    };

    private static WorkflowTemplate SequentialPipeline() => new()
    {
        Id = "sequential-pipeline",
        Name = "Sequential Pipeline",
        Description = "A 3-step linear flow demonstrating sequential step execution.",
        Category = "Getting Started",
        Tags = ["beginner", "sequential", "pipeline"],
        Difficulty = TemplateDifficulty.Beginner,
        StepCount = 3,
        Definition = new WorkflowDefinitionDto
        {
            Name = "SequentialPipeline",
            Steps =
            [
                new StepDefinitionDto { Name = "Step1_Prepare", Type = "Action" },
                new StepDefinitionDto { Name = "Step2_Process", Type = "Action" },
                new StepDefinitionDto { Name = "Step3_Finalize", Type = "Action" }
            ]
        }
    };

    private static WorkflowTemplate ConditionalBranching() => new()
    {
        Id = "conditional-branching",
        Name = "Conditional Branching",
        Description = "If/else branching with different execution paths based on a condition.",
        Category = "Getting Started",
        Tags = ["beginner", "conditional", "branching"],
        Difficulty = TemplateDifficulty.Beginner,
        StepCount = 4,
        Definition = new WorkflowDefinitionDto
        {
            Name = "ConditionalBranching",
            Steps =
            [
                new StepDefinitionDto { Name = "ValidateInput", Type = "Action" },
                new StepDefinitionDto
                {
                    Name = "CheckCondition",
                    Type = "Conditional",
                    Then = new StepDefinitionDto { Name = "ProcessValid", Type = "Action" },
                    Else = new StepDefinitionDto { Name = "HandleInvalid", Type = "Action" }
                },
                new StepDefinitionDto { Name = "Summary", Type = "Action" }
            ]
        }
    };

    private static WorkflowTemplate ParallelExecution() => new()
    {
        Id = "parallel-execution",
        Name = "Parallel Execution",
        Description = "Run multiple branches in parallel and wait for all to complete before continuing.",
        Category = "Getting Started",
        Tags = ["beginner", "parallel", "concurrency"],
        Difficulty = TemplateDifficulty.Beginner,
        StepCount = 5,
        Definition = new WorkflowDefinitionDto
        {
            Name = "ParallelExecution",
            Steps =
            [
                new StepDefinitionDto { Name = "PrepareData", Type = "Action" },
                new StepDefinitionDto
                {
                    Name = "ParallelBranches",
                    Type = "Parallel",
                    Steps =
                    [
                        new StepDefinitionDto { Name = "BranchA_FetchExternal", Type = "Action" },
                        new StepDefinitionDto { Name = "BranchB_ComputeLocal", Type = "Action" },
                        new StepDefinitionDto { Name = "BranchC_ValidateRules", Type = "Action" }
                    ]
                },
                new StepDefinitionDto { Name = "MergeResults", Type = "Action" }
            ]
        }
    };

    private static WorkflowTemplate ErrorHandling() => new()
    {
        Id = "error-handling",
        Name = "Error Handling",
        Description = "Try/catch/finally pattern for robust error handling in workflows.",
        Category = "Getting Started",
        Tags = ["intermediate", "error-handling", "try-catch"],
        Difficulty = TemplateDifficulty.Intermediate,
        StepCount = 5,
        Definition = new WorkflowDefinitionDto
        {
            Name = "ErrorHandling",
            Steps =
            [
                new StepDefinitionDto
                {
                    Name = "SafeOperation",
                    Type = "TryCatch",
                    TryBody =
                    [
                        new StepDefinitionDto { Name = "RiskyStep", Type = "Action" },
                        new StepDefinitionDto { Name = "DependentStep", Type = "Action" }
                    ],
                    CatchTypes = ["System.InvalidOperationException", "System.TimeoutException"],
                    FinallyBody =
                    [
                        new StepDefinitionDto { Name = "CleanupResources", Type = "Action" }
                    ]
                },
                new StepDefinitionDto { Name = "Continue", Type = "Action" }
            ]
        }
    };

    private static WorkflowTemplate RetryWithBackoff() => new()
    {
        Id = "retry-with-backoff",
        Name = "Retry with Backoff",
        Description = "Wrap a flaky operation in a retry step with configurable max attempts.",
        Category = "Getting Started",
        Tags = ["intermediate", "retry", "resilience"],
        Difficulty = TemplateDifficulty.Intermediate,
        StepCount = 3,
        Definition = new WorkflowDefinitionDto
        {
            Name = "RetryWithBackoff",
            Steps =
            [
                new StepDefinitionDto { Name = "SetupConnection", Type = "Action" },
                new StepDefinitionDto
                {
                    Name = "RetryableCall",
                    Type = "Retry",
                    MaxAttempts = 3,
                    Steps = [new StepDefinitionDto { Name = "CallExternalApi", Type = "Action" }]
                },
                new StepDefinitionDto { Name = "ProcessResponse", Type = "Action" }
            ]
        }
    };

    private static WorkflowTemplate LoopProcessing() => new()
    {
        Id = "loop-processing",
        Name = "Loop Processing",
        Description = "ForEach and While loop patterns for iterative data processing.",
        Category = "Getting Started",
        Tags = ["intermediate", "loop", "foreach", "while"],
        Difficulty = TemplateDifficulty.Intermediate,
        StepCount = 4,
        Definition = new WorkflowDefinitionDto
        {
            Name = "LoopProcessing",
            Steps =
            [
                new StepDefinitionDto { Name = "LoadItems", Type = "Action" },
                new StepDefinitionDto
                {
                    Name = "ProcessEachItem",
                    Type = "ForEach",
                    Steps =
                    [
                        new StepDefinitionDto { Name = "TransformItem", Type = "Action" },
                        new StepDefinitionDto { Name = "ValidateItem", Type = "Action" }
                    ]
                },
                new StepDefinitionDto
                {
                    Name = "PollUntilComplete",
                    Type = "While",
                    Steps = [new StepDefinitionDto { Name = "CheckStatus", Type = "Action" }]
                },
                new StepDefinitionDto { Name = "Summarize", Type = "Action" }
            ]
        }
    };

    // ── Data Processing ─────────────────────────────────────────

    private static WorkflowTemplate CsvEtlPipeline() => new()
    {
        Id = "csv-etl-pipeline",
        Name = "CSV ETL Pipeline",
        Description = "Extract CSV data, transform and filter records, validate, and write output.",
        Category = "Data Processing",
        Tags = ["etl", "csv", "transform", "pipeline"],
        Difficulty = TemplateDifficulty.Intermediate,
        StepCount = 4,
        Definition = new WorkflowDefinitionDto
        {
            Name = "CsvEtlPipeline",
            Steps =
            [
                new StepDefinitionDto { Name = "Extract", Type = "Action" },
                new StepDefinitionDto { Name = "Transform", Type = "DataMapStep" },
                new StepDefinitionDto { Name = "Validate", Type = "Action" },
                new StepDefinitionDto { Name = "Load", Type = "Action" }
            ]
        }
    };

    private static WorkflowTemplate DataMappingTransform() => new()
    {
        Id = "data-mapping-transform",
        Name = "Data Mapping & Transform",
        Description = "Use DataMapStep to map and transform fields between data formats.",
        Category = "Data Processing",
        Tags = ["data-mapping", "transform", "fields"],
        Difficulty = TemplateDifficulty.Intermediate,
        StepCount = 3,
        Definition = new WorkflowDefinitionDto
        {
            Name = "DataMappingTransform",
            Steps =
            [
                new StepDefinitionDto { Name = "ReadSource", Type = "Action" },
                new StepDefinitionDto { Name = "MapFields", Type = "DataMapStep" },
                new StepDefinitionDto { Name = "WriteTarget", Type = "Action" }
            ]
        }
    };

    private static WorkflowTemplate SchemaValidation() => new()
    {
        Id = "schema-validation",
        Name = "Schema Validation",
        Description = "Validate data against a JSON schema with conditional error handling.",
        Category = "Data Processing",
        Tags = ["validation", "schema", "json"],
        Difficulty = TemplateDifficulty.Intermediate,
        StepCount = 4,
        Definition = new WorkflowDefinitionDto
        {
            Name = "SchemaValidation",
            Steps =
            [
                new StepDefinitionDto { Name = "LoadData", Type = "Action" },
                new StepDefinitionDto { Name = "ValidateSchema", Type = "Action" },
                new StepDefinitionDto
                {
                    Name = "CheckValid",
                    Type = "Conditional",
                    Then = new StepDefinitionDto { Name = "ProcessData", Type = "Action" },
                    Else = new StepDefinitionDto { Name = "ReportErrors", Type = "Action" }
                }
            ]
        }
    };

    // ── Order Management ────────────────────────────────────────

    private static WorkflowTemplate OrderProcessingSaga() => new()
    {
        Id = "order-processing-saga",
        Name = "Order Processing Saga",
        Description = "Multi-step order saga with inventory reservation, payment charging, and compensation on failure.",
        Category = "Order Management",
        Tags = ["saga", "compensation", "order", "transaction"],
        Difficulty = TemplateDifficulty.Advanced,
        StepCount = 6,
        Definition = new WorkflowDefinitionDto
        {
            Name = "OrderProcessingSaga",
            Steps =
            [
                new StepDefinitionDto { Name = "ValidateOrder", Type = "Action" },
                new StepDefinitionDto
                {
                    Name = "OrderSaga",
                    Type = "Saga",
                    Steps =
                    [
                        new StepDefinitionDto { Name = "CheckInventory", Type = "Action" },
                        new StepDefinitionDto { Name = "ChargePayment", Type = "Action" },
                        new StepDefinitionDto { Name = "ShipOrder", Type = "Action" }
                    ]
                },
                new StepDefinitionDto { Name = "SendConfirmation", Type = "Action" }
            ]
        }
    };

    private static WorkflowTemplate ExpressOrderFlow() => new()
    {
        Id = "express-order-flow",
        Name = "Express Order Flow",
        Description = "Fast-path conditional routing — express orders get prioritized, standard orders follow normal processing.",
        Category = "Order Management",
        Tags = ["conditional", "routing", "order", "express"],
        Difficulty = TemplateDifficulty.Intermediate,
        StepCount = 5,
        Definition = new WorkflowDefinitionDto
        {
            Name = "ExpressOrderFlow",
            Steps =
            [
                new StepDefinitionDto { Name = "ValidateOrder", Type = "Action" },
                new StepDefinitionDto { Name = "CheckInventory", Type = "Action" },
                new StepDefinitionDto
                {
                    Name = "ShippingRoute",
                    Type = "Conditional",
                    Then = new StepDefinitionDto { Name = "PrioritizeOrder", Type = "Action" },
                    Else = new StepDefinitionDto { Name = "StandardProcessing", Type = "Action" }
                },
                new StepDefinitionDto { Name = "ChargePayment", Type = "Action" },
                new StepDefinitionDto { Name = "SendConfirmation", Type = "Action" }
            ]
        }
    };

    private static WorkflowTemplate OrderWithApproval() => new()
    {
        Id = "order-with-approval",
        Name = "Order with Approval",
        Description = "Order workflow with a human approval gate for high-value orders.",
        Category = "Order Management",
        Tags = ["approval", "human-task", "order", "gate"],
        Difficulty = TemplateDifficulty.Advanced,
        StepCount = 6,
        Definition = new WorkflowDefinitionDto
        {
            Name = "OrderWithApproval",
            Steps =
            [
                new StepDefinitionDto { Name = "ValidateOrder", Type = "Action" },
                new StepDefinitionDto
                {
                    Name = "CheckApprovalNeeded",
                    Type = "Conditional",
                    Then = new StepDefinitionDto { Name = "ManagerApproval", Type = "ApprovalStep" },
                    Else = new StepDefinitionDto { Name = "AutoApprove", Type = "Action" }
                },
                new StepDefinitionDto { Name = "CheckInventory", Type = "Action" },
                new StepDefinitionDto { Name = "ChargePayment", Type = "Action" },
                new StepDefinitionDto { Name = "SendConfirmation", Type = "Action" }
            ]
        }
    };

    // ── AI & Agents ─────────────────────────────────────────────

    private static WorkflowTemplate TaskExtractionPipeline() => new()
    {
        Id = "task-extraction-pipeline",
        Name = "Task Extraction Pipeline",
        Description = "AI-powered pipeline that collects text from sources, normalizes input, extracts tasks via LLM, validates, and persists.",
        Category = "AI & Agents",
        Tags = ["ai", "extraction", "llm", "pipeline"],
        Difficulty = TemplateDifficulty.Advanced,
        StepCount = 5,
        Definition = new WorkflowDefinitionDto
        {
            Name = "TaskExtractionPipeline",
            Steps =
            [
                new StepDefinitionDto { Name = "CollectSources", Type = "Action" },
                new StepDefinitionDto { Name = "NormalizeInput", Type = "Action" },
                new StepDefinitionDto { Name = "ExtractTodos", Type = "LlmCallStep" },
                new StepDefinitionDto { Name = "ValidateAndDeduplicate", Type = "Action" },
                new StepDefinitionDto { Name = "PersistTodos", Type = "Action" }
            ]
        }
    };

    private static WorkflowTemplate AgentTriageWorkflow() => new()
    {
        Id = "agent-triage-workflow",
        Name = "Agent Triage Workflow",
        Description = "Agent loop for triaging tasks by priority, with parallel branches for agent execution and human task enrichment.",
        Category = "AI & Agents",
        Tags = ["ai", "agent", "triage", "parallel"],
        Difficulty = TemplateDifficulty.Advanced,
        StepCount = 4,
        Definition = new WorkflowDefinitionDto
        {
            Name = "AgentTriageWorkflow",
            Steps =
            [
                new StepDefinitionDto { Name = "TriageTasks", Type = "AgentDecisionStep" },
                new StepDefinitionDto
                {
                    Name = "ExecuteAndEnrich",
                    Type = "Parallel",
                    Steps =
                    [
                        new StepDefinitionDto { Name = "AgentExecution", Type = "AgentLoopStep" },
                        new StepDefinitionDto { Name = "EnrichHumanTasks", Type = "HumanTaskStep" }
                    ]
                },
                new StepDefinitionDto { Name = "AggregateResults", Type = "Action" }
            ]
        }
    };

    // ── Voice & Audio ───────────────────────────────────────────

    private static WorkflowTemplate QuickTranscript() => new()
    {
        Id = "quick-transcript",
        Name = "Quick Transcript",
        Description = "Record audio, transcribe it, clean up with LLM, and present for human review.",
        Category = "Voice & Audio",
        Tags = ["voice", "transcription", "llm", "review"],
        Difficulty = TemplateDifficulty.Intermediate,
        StepCount = 5,
        Definition = new WorkflowDefinitionDto
        {
            Name = "QuickTranscript",
            Steps =
            [
                new StepDefinitionDto { Name = "RecordAudio", Type = "Action" },
                new StepDefinitionDto { Name = "Transcribe", Type = "Action" },
                new StepDefinitionDto { Name = "LlmCleanup", Type = "LlmCallStep" },
                new StepDefinitionDto { Name = "StoreCleanup", Type = "Action" },
                new StepDefinitionDto { Name = "ReviewTranscript", Type = "HumanTaskStep" }
            ]
        }
    };

    private static WorkflowTemplate MeetingNotes() => new()
    {
        Id = "meeting-notes",
        Name = "Meeting Notes",
        Description = "Transcribe a meeting, identify speakers, extract formatted notes and action items, then review.",
        Category = "Voice & Audio",
        Tags = ["voice", "meeting", "speakers", "action-items"],
        Difficulty = TemplateDifficulty.Advanced,
        StepCount = 7,
        Definition = new WorkflowDefinitionDto
        {
            Name = "MeetingNotes",
            Steps =
            [
                new StepDefinitionDto { Name = "Transcribe", Type = "Action" },
                new StepDefinitionDto { Name = "CountSpeakers", Type = "Action" },
                new StepDefinitionDto { Name = "LabelSpeakers", Type = "Action" },
                new StepDefinitionDto { Name = "FormatMeetingNotes", Type = "LlmCallStep" },
                new StepDefinitionDto { Name = "ExtractActionItems", Type = "LlmCallStep" },
                new StepDefinitionDto { Name = "StoreResults", Type = "Action" },
                new StepDefinitionDto { Name = "ReviewMeetingNotes", Type = "HumanTaskStep" }
            ]
        }
    };

    private static WorkflowTemplate BlogFromInterview() => new()
    {
        Id = "blog-from-interview",
        Name = "Blog from Interview",
        Description = "5-phase agentic workflow: record topic, generate questions via agent loop, record answers, synthesize blog post, and review.",
        Category = "Voice & Audio",
        Tags = ["voice", "agent", "blog", "interview", "compaction"],
        Difficulty = TemplateDifficulty.Advanced,
        StepCount = 10,
        Definition = new WorkflowDefinitionDto
        {
            Name = "BlogInterview",
            Steps =
            [
                // Phase 1
                new StepDefinitionDto { Name = "RecordTopicIntro", Type = "Action" },
                new StepDefinitionDto { Name = "TranscribeTopic", Type = "Action" },
                new StepDefinitionDto { Name = "CleanupTopic", Type = "LlmCallStep" },
                new StepDefinitionDto { Name = "ReviewTopic", Type = "HumanTaskStep" },
                // Phase 2
                new StepDefinitionDto { Name = "GenerateQuestions", Type = "AgentLoopStep" },
                new StepDefinitionDto { Name = "ParseQuestions", Type = "Action" },
                // Phase 3
                new StepDefinitionDto { Name = "RecordAnswers", Type = "Action" },
                // Phase 4
                new StepDefinitionDto { Name = "SynthesizeBlog", Type = "AgentLoopStep" },
                new StepDefinitionDto { Name = "StoreBlogPost", Type = "Action" },
                // Phase 5
                new StepDefinitionDto { Name = "ReviewBlog", Type = "HumanTaskStep" }
            ]
        }
    };

    private static WorkflowTemplate BrainDumpSynthesis() => new()
    {
        Id = "brain-dump-synthesis",
        Name = "Brain Dump Synthesis",
        Description = "Record unstructured audio, transcribe, clean up, synthesize into a structured document via agent loop.",
        Category = "Voice & Audio",
        Tags = ["voice", "agent", "synthesis", "unstructured"],
        Difficulty = TemplateDifficulty.Advanced,
        StepCount = 7,
        Definition = new WorkflowDefinitionDto
        {
            Name = "BrainDumpSynthesis",
            Steps =
            [
                new StepDefinitionDto { Name = "RecordBrainDump", Type = "Action" },
                new StepDefinitionDto { Name = "Transcribe", Type = "Action" },
                new StepDefinitionDto { Name = "LlmCleanup", Type = "LlmCallStep" },
                new StepDefinitionDto { Name = "ReviewCleanup", Type = "HumanTaskStep" },
                new StepDefinitionDto { Name = "Synthesize", Type = "AgentLoopStep" },
                new StepDefinitionDto { Name = "StoreOutput", Type = "Action" },
                new StepDefinitionDto { Name = "ReviewFinal", Type = "HumanTaskStep" }
            ]
        }
    };

    private static WorkflowTemplate PodcastTranscript() => new()
    {
        Id = "podcast-transcript",
        Name = "Podcast Transcript",
        Description = "Transcribe a podcast, label speakers, then parallel-branch for summary and full transcript formatting before merging.",
        Category = "Voice & Audio",
        Tags = ["voice", "podcast", "parallel", "summary"],
        Difficulty = TemplateDifficulty.Advanced,
        StepCount = 6,
        Definition = new WorkflowDefinitionDto
        {
            Name = "PodcastTranscript",
            Steps =
            [
                new StepDefinitionDto { Name = "Transcribe", Type = "Action" },
                new StepDefinitionDto { Name = "LabelSpeakers", Type = "Action" },
                new StepDefinitionDto
                {
                    Name = "ParallelProcessing",
                    Type = "Parallel",
                    Steps =
                    [
                        new StepDefinitionDto { Name = "Summarize", Type = "LlmCallStep" },
                        new StepDefinitionDto { Name = "FormatTranscript", Type = "LlmCallStep" }
                    ]
                },
                new StepDefinitionDto { Name = "MergeResults", Type = "Action" },
                new StepDefinitionDto { Name = "ReviewPodcast", Type = "HumanTaskStep" }
            ]
        }
    };

    // ── Integration Patterns ────────────────────────────────────

    private static WorkflowTemplate ContentBasedRouter() => new()
    {
        Id = "content-based-router",
        Name = "Content-Based Router",
        Description = "Route incoming messages to different processing steps based on message type or content.",
        Category = "Integration Patterns",
        Tags = ["integration", "routing", "eip", "conditional"],
        Difficulty = TemplateDifficulty.Intermediate,
        StepCount = 4,
        Definition = new WorkflowDefinitionDto
        {
            Name = "ContentBasedRouter",
            Steps =
            [
                new StepDefinitionDto { Name = "ReceiveMessage", Type = "Action" },
                new StepDefinitionDto { Name = "ClassifyMessage", Type = "Action" },
                new StepDefinitionDto
                {
                    Name = "RouteByType",
                    Type = "ContentBasedRouter",
                    Steps =
                    [
                        new StepDefinitionDto { Name = "HandleOrderMessage", Type = "Action" },
                        new StepDefinitionDto { Name = "HandleInventoryMessage", Type = "Action" },
                        new StepDefinitionDto { Name = "HandleNotificationMessage", Type = "Action" }
                    ]
                },
                new StepDefinitionDto { Name = "LogRouting", Type = "Action" }
            ]
        }
    };

    private static WorkflowTemplate ScatterGather() => new()
    {
        Id = "scatter-gather",
        Name = "Scatter-Gather",
        Description = "Fan out a request to multiple services in parallel, then aggregate all responses.",
        Category = "Integration Patterns",
        Tags = ["integration", "scatter-gather", "parallel", "aggregation"],
        Difficulty = TemplateDifficulty.Advanced,
        StepCount = 5,
        Definition = new WorkflowDefinitionDto
        {
            Name = "ScatterGather",
            Steps =
            [
                new StepDefinitionDto { Name = "PrepareRequest", Type = "Action" },
                new StepDefinitionDto
                {
                    Name = "ScatterToServices",
                    Type = "Parallel",
                    Steps =
                    [
                        new StepDefinitionDto { Name = "CallServiceA", Type = "HttpStep" },
                        new StepDefinitionDto { Name = "CallServiceB", Type = "HttpStep" },
                        new StepDefinitionDto { Name = "CallServiceC", Type = "HttpStep" }
                    ]
                },
                new StepDefinitionDto { Name = "AggregateResponses", Type = "Action" },
                new StepDefinitionDto { Name = "SelectBestResult", Type = "Action" }
            ]
        }
    };

    private static WorkflowTemplate PublishSubscribe() => new()
    {
        Id = "publish-subscribe",
        Name = "Publish-Subscribe",
        Description = "Event-driven workflow that publishes events and has parallel subscribers reacting to them.",
        Category = "Integration Patterns",
        Tags = ["integration", "events", "pub-sub", "parallel"],
        Difficulty = TemplateDifficulty.Intermediate,
        StepCount = 5,
        Definition = new WorkflowDefinitionDto
        {
            Name = "PublishSubscribe",
            Steps =
            [
                new StepDefinitionDto { Name = "ProcessInput", Type = "Action" },
                new StepDefinitionDto { Name = "PublishEvent", Type = "PublishEventStep" },
                new StepDefinitionDto
                {
                    Name = "Subscribers",
                    Type = "Parallel",
                    Steps =
                    [
                        new StepDefinitionDto { Name = "NotificationSubscriber", Type = "Action" },
                        new StepDefinitionDto { Name = "AuditLogSubscriber", Type = "Action" },
                        new StepDefinitionDto { Name = "AnalyticsSubscriber", Type = "Action" }
                    ]
                },
                new StepDefinitionDto { Name = "ConfirmDelivery", Type = "Action" }
            ]
        }
    };

    private static WorkflowTemplate HttpApiOrchestration() => new()
    {
        Id = "http-api-orchestration",
        Name = "HTTP API Orchestration",
        Description = "Chain multiple REST API calls with data mapping between each step.",
        Category = "Integration Patterns",
        Tags = ["integration", "http", "rest", "orchestration"],
        Difficulty = TemplateDifficulty.Intermediate,
        StepCount = 5,
        Definition = new WorkflowDefinitionDto
        {
            Name = "HttpApiOrchestration",
            Steps =
            [
                new StepDefinitionDto { Name = "FetchUserProfile", Type = "HttpStep" },
                new StepDefinitionDto { Name = "MapUserData", Type = "DataMapStep" },
                new StepDefinitionDto { Name = "FetchUserOrders", Type = "HttpStep" },
                new StepDefinitionDto { Name = "EnrichOrderData", Type = "DataMapStep" },
                new StepDefinitionDto { Name = "SendSummaryEmail", Type = "HttpStep" }
            ]
        }
    };

    private static WorkflowTemplate WebhookHandler() => new()
    {
        Id = "webhook-handler",
        Name = "Webhook Handler",
        Description = "Trigger a workflow from an incoming webhook, validate the payload, process, and respond.",
        Category = "Integration Patterns",
        Tags = ["integration", "webhook", "trigger", "http"],
        Difficulty = TemplateDifficulty.Intermediate,
        StepCount = 5,
        Definition = new WorkflowDefinitionDto
        {
            Name = "WebhookHandler",
            Steps =
            [
                new StepDefinitionDto { Name = "ReceiveWebhook", Type = "Action" },
                new StepDefinitionDto { Name = "ValidateSignature", Type = "Action" },
                new StepDefinitionDto { Name = "ParsePayload", Type = "DataMapStep" },
                new StepDefinitionDto
                {
                    Name = "RouteByEvent",
                    Type = "Conditional",
                    Then = new StepDefinitionDto { Name = "HandleCreated", Type = "Action" },
                    Else = new StepDefinitionDto { Name = "HandleUpdated", Type = "Action" }
                },
                new StepDefinitionDto { Name = "SendAcknowledgement", Type = "HttpStep" }
            ]
        }
    };
}
