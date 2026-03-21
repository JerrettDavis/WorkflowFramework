using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace WorkflowFramework.Dashboard.Tests;

#if NET10_0
public sealed class WorkflowDesignerInteropContractTests
{
    private static readonly Assembly WebAssembly = LoadWebAssembly();
    private static readonly Type WorkflowDesignerType = FindTypeByName("WorkflowDesigner");
    private static readonly Type PropertiesPanelType = FindTypeByName("PropertiesPanel");

    [Fact]
    public void WorkflowDesigner_Exposes_OnSelectionChanged_AsJsInvokable()
    {
        var method = WorkflowDesignerType.GetMethod("OnSelectionChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        method!.GetCustomAttributes(inherit: true)
            .Select(a => a.GetType().Name)
            .Should()
            .Contain("JSInvokableAttribute");
    }

    [Fact]
    public void WorkflowDesigner_Exposes_OnEdgeCreated_WithSourceHandle_AsJsInvokable()
    {
        var method = WorkflowDesignerType.GetMethod("OnEdgeCreated", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        method!.GetParameters().Select(parameter => parameter.ParameterType)
            .Should()
            .ContainInOrder(typeof(string), typeof(string), typeof(string));
        method.GetCustomAttributes(inherit: true)
            .Select(a => a.GetType().Name)
            .Should()
            .Contain("JSInvokableAttribute");
    }

    [Fact]
    public void BuildRunAssistantTasks_FindsRecordTask_ForBlogInterviewTemplate()
    {
        var workflowDefinition = CreateWorkflowDefinition(
            "BlogInterview",
            CreateStep("RecordTopicIntro", "Action"),
            CreateStep("TranscribeTopic", "Action"),
            CreateStep("GenerateQuestions", "AgentLoopStep"),
            CreateStep("RecordAnswers", "Action"),
            CreateStep("ReviewBlog", "HumanTaskStep"));

        var buildTasks = WorkflowDesignerType.GetMethod("BuildRunAssistantTasks", BindingFlags.Static | BindingFlags.NonPublic);
        buildTasks.Should().NotBeNull();

        var tasks = ((IEnumerable?)buildTasks!.Invoke(null, [workflowDefinition]))?.Cast<object>().ToList();
        tasks.Should().NotBeNull();
        tasks!.Should().NotBeEmpty();
        tasks.Count.Should().BeGreaterThanOrEqualTo(5);
        tasks.Any(task => string.Equals(GetProperty(task, "Kind")?.ToString(), "Record", StringComparison.Ordinal)).Should().BeTrue();
        tasks.Any(task => string.Equals(GetProperty(task, "Kind")?.ToString(), "Transcript", StringComparison.Ordinal)).Should().BeTrue();
        tasks.Any(task => string.Equals(GetProperty(task, "Kind")?.ToString(), "Questions", StringComparison.Ordinal)).Should().BeTrue();
        tasks.Any(task => string.Equals(GetProperty(task, "Kind")?.ToString(), "QaAnswers", StringComparison.Ordinal)).Should().BeTrue();
        tasks.Any(task => string.Equals(GetProperty(task, "Kind")?.ToString(), "Review", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Fact]
    public void BuildRunAssistantTasks_DoesNotTreatPromptOnlyAiSteps_AsQuestionPlanning()
    {
        var llmStep = CreateStep("Summarize", "LlmCallStep");
        SetProperty(llmStep, "Config", new Dictionary<string, string>
        {
            ["provider"] = "openai",
            ["model"] = "gpt-4o-mini",
            ["prompt"] = "Summarize the latest note"
        });

        var workflowDefinition = CreateWorkflowDefinition("Plain AI Flow", llmStep);
        var buildTasks = WorkflowDesignerType.GetMethod("BuildRunAssistantTasks", BindingFlags.Static | BindingFlags.NonPublic);
        buildTasks.Should().NotBeNull();

        var tasks = ((IEnumerable?)buildTasks!.Invoke(null, [workflowDefinition]))?.Cast<object>().ToList();
        tasks.Should().NotBeNull();
        tasks.Should().BeEmpty();
    }

    [Fact]
    public void BuildRunAssistantRequest_IncludesRecordedAudioBase64Payload()
    {
        var designer = Activator.CreateInstance(WorkflowDesignerType);
        designer.Should().NotBeNull();

        WorkflowDesignerType.GetField("_runTranscript", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(designer, "Transcript sample");

        var payloadType = WorkflowDesignerType.GetNestedType("BrowserAudioPayload", BindingFlags.NonPublic);
        payloadType.Should().NotBeNull();
        var payload = Activator.CreateInstance(payloadType!);
        payloadType!.GetProperty("FileName")!.SetValue(payload, "clip.wav");
        payloadType.GetProperty("MimeType")!.SetValue(payload, "audio/wav");
        payloadType.GetProperty("Size")!.SetValue(payload, 123L);
        payloadType.GetProperty("Base64")!.SetValue(payload, "QUJD");

        var recordingsField = WorkflowDesignerType.GetField("_runAudioByStep", BindingFlags.Instance | BindingFlags.NonPublic);
        recordingsField.Should().NotBeNull();
        var recordingsMap = recordingsField!.GetValue(designer);
        recordingsMap.Should().NotBeNull();
        recordingsMap!.GetType().GetMethod("set_Item")!.Invoke(recordingsMap, ["RecordTopicIntro", payload]);

        var buildRequest = WorkflowDesignerType.GetMethod("BuildRunAssistantRequest", BindingFlags.Instance | BindingFlags.NonPublic);
        buildRequest.Should().NotBeNull();
        var request = buildRequest!.Invoke(designer, null);
        request.Should().NotBeNull();

        var inputs = GetProperty(request!, "Inputs") as IDictionary;
        inputs.Should().NotBeNull();
        inputs!.Contains("recordings").Should().BeTrue();

        var recordings = inputs["recordings"] as IDictionary;
        recordings.Should().NotBeNull();
        recordings!.Count.Should().Be(1);

        var firstRecording = recordings.Values.Cast<object>().FirstOrDefault() as IDictionary;
        firstRecording.Should().NotBeNull();
        firstRecording!.Contains("base64").Should().BeTrue();
        firstRecording["base64"]?.ToString().Should().Be("QUJD");
    }

    [Fact]
    public async Task OnSelectionChanged_ClearsSelectedNodeState_WhenSelectionIsEmpty()
    {
        var designer = Activator.CreateInstance(WorkflowDesignerType);
        designer.Should().NotBeNull();

        WorkflowDesignerType.GetField("_selectedNodeId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(designer, "node-1");
        WorkflowDesignerType.GetField("_selectedNodeType", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(designer, "Action");
        WorkflowDesignerType.GetField("_selectedNodeConfig", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(designer, new Dictionary<string, object?> { ["label"] = "Action" });
        var selectedConnectionsField = WorkflowDesignerType.GetField("_selectedNodeConnections", BindingFlags.Instance | BindingFlags.NonPublic)!;
        selectedConnectionsField.SetValue(designer, Activator.CreateInstance(selectedConnectionsField.FieldType));

        var onSelectionChanged = WorkflowDesignerType.GetMethod("OnSelectionChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        onSelectionChanged.Should().NotBeNull();

        var task = onSelectionChanged!.Invoke(designer, [Array.Empty<string>()]) as Task;
        task.Should().NotBeNull();
        await task!;

        WorkflowDesignerType.GetField("_selectedNodeId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(designer).Should().BeNull();
        WorkflowDesignerType.GetField("_selectedNodeType", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(designer).Should().BeNull();
        selectedConnectionsField.GetValue(designer).Should().BeNull();

        var selectedConfig = WorkflowDesignerType.GetField("_selectedNodeConfig", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(designer) as IDictionary;
        selectedConfig.Should().NotBeNull();
        selectedConfig!.Count.Should().Be(0);
    }

    [Fact]
    public void CanvasToDefinition_UsesConfiguredNodeLabel_AndPreservesCanvasSnapshot()
    {
        var designer = Activator.CreateInstance(WorkflowDesignerType);
        designer.Should().NotBeNull();

        WorkflowDesignerType.GetField("_workflowName", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(designer, "Agent Flow");

        using var document = JsonDocument.Parse("""
            {
              "nodes": [
                {
                  "id": "node_1",
                  "type": "Action",
                  "label": "Action",
                  "x": 180,
                  "y": 120,
                  "config": {
                    "label": "Summarize transcript",
                    "_notes": "Designer-only note",
                    "provider": "ollama"
                  }
                }
              ],
              "edges": []
            }
            """);

        var canvasToDefinition = WorkflowDesignerType.GetMethod("CanvasToDefinition", BindingFlags.Instance | BindingFlags.NonPublic);
        canvasToDefinition.Should().NotBeNull();

        var definition = canvasToDefinition!.Invoke(designer, [document.RootElement]);
        definition.Should().NotBeNull();

        var steps = GetProperty(definition!, "Steps") as IList;
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(1);

        var firstStep = steps[0];
        firstStep.Should().NotBeNull();
        GetProperty(firstStep, "Name")?.ToString().Should().Be("Summarize transcript");

        var stepConfig = GetProperty(firstStep, "Config") as IDictionary;
        stepConfig.Should().NotBeNull();
        stepConfig!.Contains("provider").Should().BeTrue();
        stepConfig.Contains("label").Should().BeFalse();
        stepConfig.Contains("_notes").Should().BeFalse();

        var canvas = GetProperty(definition, "Canvas");
        canvas.Should().NotBeNull();

        var canvasNodes = GetProperty(canvas!, "Nodes") as IList;
        canvasNodes.Should().NotBeNull();
        canvasNodes!.Count.Should().Be(1);
        GetProperty(canvasNodes[0], "Label")?.ToString().Should().Be("Summarize transcript");
    }

    [Fact]
    public void WebWorkflowDefinitionDto_PreservesCanonicalDelayAndSubWorkflowFields()
    {
        const string json = """
            {
              "name": "Parity",
              "steps": [
                {
                  "name": "Delay child",
                  "type": "Delay",
                  "delaySeconds": 5,
                  "subWorkflowName": "child-flow"
                }
              ]
            }
            """;
        var webDefinition = JsonSerializer.Deserialize(json, FindTypeByName("WorkflowDefinitionDto"));

        webDefinition.Should().NotBeNull();
        var steps = GetProperty(webDefinition!, "Steps") as IList;
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(1);
        GetProperty(steps[0], "DelaySeconds").Should().Be(5d);
        GetProperty(steps[0], "SubWorkflowName")?.ToString().Should().Be("child-flow");
    }

    [Fact]
    public void StepCatalog_FallbackProperties_UseSerializerCompatibleFieldNames()
    {
        var stepCatalogType = FindTypeByName("StepCatalog");
        var getAll = stepCatalogType.GetMethod("GetAll", BindingFlags.Static | BindingFlags.Public);
        getAll.Should().NotBeNull();

        var definitions = ((IEnumerable?)getAll!.Invoke(null, null))?.Cast<object>().ToList();
        definitions.Should().NotBeNull();

        IReadOnlyList<string> PropertyNamesFor(string stepType)
            => ((GetProperty(definitions!.Single(d => string.Equals(GetProperty(d, "Type")?.ToString(), stepType, StringComparison.Ordinal)), "Properties") as IEnumerable)
                ?? Array.Empty<object>())
                .Cast<object>()
                .Select(p => GetProperty(p, "Name")?.ToString())
                .OfType<string>()
                .ToList();

        PropertyNamesFor("Timeout").Should().Contain("timeoutSeconds");
        PropertyNamesFor("Timeout").Should().NotContain("durationMs");
        PropertyNamesFor("Delay").Should().Contain("delaySeconds");
        PropertyNamesFor("Delay").Should().NotContain("durationMs");
        PropertyNamesFor("SubWorkflow").Should().Contain("subWorkflowName");
        PropertyNamesFor("SubWorkflow").Should().NotContain("workflowName");
        PropertyNamesFor("Conditional").Should().Contain("expression");
        PropertyNamesFor("Conditional").Should().NotContain("thenStep");
        PropertyNamesFor("Conditional").Should().NotContain("elseStep");
        PropertyNamesFor("Retry").Should().Contain("maxAttempts");
        PropertyNamesFor("Retry").Should().NotContain("delayMs");
        PropertyNamesFor("Retry").Should().NotContain("backoffMultiplier");
        PropertyNamesFor("HttpStep").Should().Contain(["url", "method", "headers", "body", "contentType"]);
        PropertyNamesFor("HttpStep").Should().NotContain("timeoutMs");
    }

    [Fact]
    public void StepCatalog_FallbackSubWorkflow_UsesWorkflowReferencePicker()
    {
        var stepCatalogType = FindTypeByName("StepCatalog");
        var getAll = stepCatalogType.GetMethod("GetAll", BindingFlags.Static | BindingFlags.Public);
        getAll.Should().NotBeNull();

        var definitions = ((IEnumerable?)getAll!.Invoke(null, null))?.Cast<object>().ToList();
        definitions.Should().NotBeNull();

        var subWorkflow = definitions!.Single(d => string.Equals(GetProperty(d, "Type")?.ToString(), "SubWorkflow", StringComparison.Ordinal));
        var properties = ((GetProperty(subWorkflow, "Properties") as IEnumerable) ?? Array.Empty<object>()).Cast<object>().ToList();
        var workflowNameProperty = properties.Single(property => string.Equals(GetProperty(property, "Name")?.ToString(), "subWorkflowName", StringComparison.Ordinal));

        GetProperty(workflowNameProperty, "UiType")?.ToString().Should().Be("workflowSelect");
    }

    [Fact]
    public void StepCatalog_FallbackAiSteps_ExposeProviderSelectOptions()
    {
        var stepCatalogType = FindTypeByName("StepCatalog");
        var getAll = stepCatalogType.GetMethod("GetAll", BindingFlags.Static | BindingFlags.Public);
        getAll.Should().NotBeNull();

        var definitions = ((IEnumerable?)getAll!.Invoke(null, null))?.Cast<object>().ToList();
        definitions.Should().NotBeNull();

        var llmCall = definitions!.Single(d => string.Equals(GetProperty(d, "Type")?.ToString(), "LlmCallStep", StringComparison.Ordinal));
        var properties = ((GetProperty(llmCall, "Properties") as IEnumerable) ?? Array.Empty<object>()).Cast<object>().ToList();
        var provider = properties.Single(property => string.Equals(GetProperty(property, "Name")?.ToString(), "provider", StringComparison.Ordinal));

        GetProperty(provider, "Type")?.ToString().Should().Be("select");
        ((GetProperty(provider, "Options") as IEnumerable) ?? Array.Empty<object>())
            .Cast<object>()
            .Select(option => option.ToString())
            .Should()
            .ContainInOrder("ollama", "openai", "anthropic", "huggingface");

        GetProperty(provider, "UiType")?.ToString().Should().Be("providerSelect");

        var model = properties.Single(property => string.Equals(GetProperty(property, "Name")?.ToString(), "model", StringComparison.Ordinal));
        GetProperty(model, "UiType")?.ToString().Should().Be("modelSelect");
        GetProperty(model, "DependsOn")?.ToString().Should().Be("provider");
        GetProperty(model, "OptionGroups").Should().NotBeNull();

        var prompt = properties.Single(property => string.Equals(GetProperty(property, "Name")?.ToString(), "prompt", StringComparison.Ordinal));
        GetProperty(prompt, "SupportsVariables").Should().Be(true);
        GetProperty(prompt, "VariableSyntax")?.ToString().Should().Contain("{{Step Name.Response}}");

        var agentLoop = definitions.Single(d => string.Equals(GetProperty(d, "Type")?.ToString(), "AgentLoopStep", StringComparison.Ordinal));
        var loopProperties = ((GetProperty(agentLoop, "Properties") as IEnumerable) ?? Array.Empty<object>()).Cast<object>().ToList();
        loopProperties.Select(property => GetProperty(property, "Name")?.ToString()).Should().NotContain("tools");
        var systemPrompt = loopProperties.Single(property => string.Equals(GetProperty(property, "Name")?.ToString(), "systemPrompt", StringComparison.Ordinal));
        GetProperty(systemPrompt, "SupportsVariables").Should().Be(true);
    }

    [Fact]
    public void BuildAvailableVariables_IncludesRunInputs_And_UpstreamStepOutputs()
    {
        using var document = JsonDocument.Parse("""
            {
              "nodes": [
                {
                  "id": "node_1",
                  "type": "HttpStep",
                  "label": "Fetch Customer",
                  "config": {
                    "label": "Fetch Customer"
                  }
                },
                {
                  "id": "node_2",
                  "type": "Action",
                  "label": "Normalize transcript",
                  "config": {
                    "label": "Normalize transcript"
                  }
                },
                {
                  "id": "node_3",
                  "type": "LlmCallStep",
                  "label": "Draft summary",
                  "config": {
                    "label": "Draft summary"
                  }
                }
              ],
              "edges": [
                { "source": "node_1", "target": "node_2" },
                { "source": "node_2", "target": "node_3" }
              ]
            }
            """);

        var buildAvailableVariables = WorkflowDesignerType.GetMethod("BuildAvailableVariables", BindingFlags.Static | BindingFlags.NonPublic);
        buildAvailableVariables.Should().NotBeNull();

        var variables = ((IEnumerable?)buildAvailableVariables!.Invoke(null, [document.RootElement, "node_3"]))?.Cast<object>().ToList();
        variables.Should().NotBeNull();

        variables!.Select(variable => GetProperty(variable, "Token")?.ToString()).Should().Contain([
            "{transcript}",
            "{{Fetch Customer.Body}}",
            "{{Normalize transcript.Output}}"
        ]);
    }

    [Fact]
    public void PropertiesPanel_GetUnresolvedVariableTokens_FlagsMissingReferences()
    {
        var variableType = PropertiesPanelType.GetNestedType("VariableReferenceInfo", BindingFlags.Public | BindingFlags.NonPublic);
        variableType.Should().NotBeNull();

        var availableVariables = Activator.CreateInstance(typeof(List<>).MakeGenericType(variableType!))!;
        AddVariable(availableVariables, variableType!, "{transcript}");
        AddVariable(availableVariables, variableType!, "{{Fetch Customer.Body}}");

        var method = PropertiesPanelType.GetMethod("GetUnresolvedVariableTokens", BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(string), typeof(IReadOnlyList<>).MakeGenericType(variableType!)], null);
        method.Should().NotBeNull();

        var unresolved = ((IEnumerable?)method!.Invoke(null, ["Use {{ Fetch Customer.Body }} and {{Missing.Response}} with {transcript}", availableVariables]))?
            .Cast<object>()
            .Select(token => token.ToString())
            .ToList();

        unresolved.Should().BeEquivalentTo(["{{Missing.Response}}"]);
    }

    [Fact]
    public void PropertiesPanel_GetWorkflowReferenceWarning_FlagsSelfReference_AndMissingSavedWorkflow()
    {
        var method = PropertiesPanelType.GetMethod(
            "GetWorkflowReferenceWarning",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            [typeof(string), typeof(string), typeof(IReadOnlyList<string>), typeof(bool)],
            null);
        method.Should().NotBeNull();

        var selfReferenceWarning = method!.Invoke(null, ["Parent Flow", "Parent Flow", new List<string> { "Child Flow" }, true]);
        selfReferenceWarning.Should().Be("Sub-workflow cannot reference the current workflow.");

        var missingWorkflowWarning = method.Invoke(null, ["Missing Flow", "Parent Flow", new List<string> { "Child Flow" }, true]);
        missingWorkflowWarning.Should().Be("No saved workflow matches this name yet. You can keep typing if the child workflow will be created later.");

        var knownWorkflowWarning = method.Invoke(null, ["Child Flow", "Parent Flow", new List<string> { "Child Flow" }, true]);
        knownWorkflowWarning.Should().BeNull();
    }

    [Fact]
    public async Task LoadDefinitionIntoCanvas_RoundTrips_ConditionalThenContinuation()
    {
        var conditional = CreateStep("Choose path", "Conditional");
        SetProperty(conditional, "Then", CreateStep("Collect answer", "Action"));

        var definition = CreateWorkflowDefinition("Conditional Flow", conditional, CreateStep("Publish update", "Action"));
        var roundTripped = await RoundTripDefinitionAsync(definition);

        var steps = GetProperty(roundTripped, "Steps") as IList;
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(2);
        GetProperty(GetProperty(steps[0], "Then")!, "Name")?.ToString().Should().Be("Collect answer");
        GetProperty(steps[1], "Name")?.ToString().Should().Be("Publish update");
    }

    [Fact]
    public void BuildCanvasGraph_UsesContinueKind_ForConditionalWithoutElse()
    {
        var conditional = CreateStep("Choose path", "Conditional");
        SetProperty(conditional, "Then", CreateStep("Collect answer", "Action"));

        var definition = CreateWorkflowDefinition("Conditional Flow", conditional, CreateStep("Publish update", "Action"));

        var buildCanvasGraph = WorkflowDesignerType.GetMethod("BuildCanvasGraph", BindingFlags.Static | BindingFlags.NonPublic);
        buildCanvasGraph.Should().NotBeNull();
        var canvas = buildCanvasGraph!.Invoke(null, [definition]);
        canvas.Should().NotBeNull();

        var edges = GetProperty(canvas!, "Edges") as IList;
        edges.Should().NotBeNull();

        edges!.Cast<object>()
            .Any(edge => string.Equals(GetProperty(edge, "Kind")?.ToString(), "continue", StringComparison.Ordinal))
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task LoadDefinitionIntoCanvas_RoundTrips_TryFinally_And_TimeoutInner()
    {
        var timeout = CreateStep("Timed summary", "Timeout");
        SetProperty(timeout, "TimeoutSeconds", 30d);
        SetProperty(timeout, "Inner", CreateStep("Summarize", "Action"));

        var tryCatch = CreateStep("Guarded task", "TryCatch");
        SetProperty(tryCatch, "TryBody", CreateList("TryBody", CreateStep("Execute task", "Action")));
        SetProperty(tryCatch, "FinallyBody", CreateList("FinallyBody", CreateStep("Cleanup", "Action")));

        var definition = CreateWorkflowDefinition("Guarded Flow", timeout, tryCatch, CreateStep("Notify", "Action"));
        var roundTripped = await RoundTripDefinitionAsync(definition);

        var steps = GetProperty(roundTripped, "Steps") as IList;
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(3);

        GetProperty(GetProperty(steps[0], "Inner")!, "Name")?.ToString().Should().Be("Summarize");
        GetProperty(steps[0], "TimeoutSeconds").Should().Be(30d);

        var tryBody = GetProperty(steps[1], "TryBody") as IList;
        var finallyBody = GetProperty(steps[1], "FinallyBody") as IList;
        tryBody.Should().NotBeNull();
        finallyBody.Should().NotBeNull();
        tryBody![0].Should().NotBeNull();
        finallyBody![0].Should().NotBeNull();
        GetProperty(tryBody[0], "Name")?.ToString().Should().Be("Execute task");
        GetProperty(finallyBody[0], "Name")?.ToString().Should().Be("Cleanup");
        GetProperty(steps[2], "Name")?.ToString().Should().Be("Notify");
    }

    [Fact]
    public async Task LoadDefinitionIntoCanvas_RoundTrips_LowercaseTimeout_WithContinuation()
    {
        var timeout = CreateStep("Timed summary", "timeout");
        SetProperty(timeout, "TimeoutSeconds", 30d);
        SetProperty(timeout, "Inner", CreateStep("Summarize", "Action"));

        var definition = CreateWorkflowDefinition("Lowercase Timeout Flow", timeout, CreateStep("Notify", "Action"));
        var roundTripped = await RoundTripDefinitionAsync(definition);

        var steps = GetProperty(roundTripped, "Steps") as IList;
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(2);
        GetProperty(GetProperty(steps[0], "Inner")!, "Name")?.ToString().Should().Be("Summarize");
        GetProperty(steps[1], "Name")?.ToString().Should().Be("Notify");
    }

    [Fact]
    public async Task LoadDefinitionIntoCanvas_RoundTrips_LowercaseConditional_WithContinuation()
    {
        var conditional = CreateStep("Choose path", "conditional");
        SetProperty(conditional, "Then", CreateStep("Collect answer", "Action"));

        var definition = CreateWorkflowDefinition("Lowercase Conditional Flow", conditional, CreateStep("Publish update", "Action"));
        var roundTripped = await RoundTripDefinitionAsync(definition);

        var steps = GetProperty(roundTripped, "Steps") as IList;
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(2);
        GetProperty(GetProperty(steps[0], "Then")!, "Name")?.ToString().Should().Be("Collect answer");
        GetProperty(steps[1], "Name")?.ToString().Should().Be("Publish update");
    }

    [Fact]
    public async Task LoadDefinitionIntoCanvas_RoundTrips_RetryBody_WithoutSwallowingSibling()
    {
        var retry = CreateStep("Retry block", "Retry");
        SetProperty(retry, "Steps", CreateList("Steps", CreateStep("Attempt task", "Action")));
        SetProperty(retry, "MaxAttempts", 3);

        var definition = CreateWorkflowDefinition("Retry Flow", retry, CreateStep("Escalate", "Action"));
        var roundTripped = await RoundTripDefinitionAsync(definition);

        var steps = GetProperty(roundTripped, "Steps") as IList;
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(2);

        var retryChildren = GetProperty(steps[0], "Steps") as IList;
        retryChildren.Should().NotBeNull();
        retryChildren!.Count.Should().Be(1);
        GetProperty(retryChildren[0], "Name")?.ToString().Should().Be("Attempt task");
        GetProperty(steps[1], "Name")?.ToString().Should().Be("Escalate");
    }

    [Fact]
    public async Task LoadDefinitionIntoCanvas_RoundTrips_LowercaseRetry_WithoutSwallowingSibling()
    {
        var retry = CreateStep("Retry block", "retry");
        SetProperty(retry, "Steps", CreateList("Steps", CreateStep("Attempt task", "Action")));
        SetProperty(retry, "MaxAttempts", 3);

        var definition = CreateWorkflowDefinition("Lowercase Retry Flow", retry, CreateStep("Escalate", "Action"));
        var roundTripped = await RoundTripDefinitionAsync(definition);

        var steps = GetProperty(roundTripped, "Steps") as IList;
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(2);

        var retryChildren = GetProperty(steps[0], "Steps") as IList;
        retryChildren.Should().NotBeNull();
        retryChildren!.Count.Should().Be(1);
        GetProperty(retryChildren[0], "Name")?.ToString().Should().Be("Attempt task");
        GetProperty(steps[1], "Name")?.ToString().Should().Be("Escalate");
    }

    [Fact]
    public async Task LoadDefinitionIntoCanvas_RoundTrips_ParallelBranches()
    {
        var parallel = CreateStep("Fan out", "Parallel");
        SetProperty(parallel, "Steps", CreateList("Steps", CreateStep("Branch A", "Action"), CreateStep("Branch B", "Action")));

        var definition = CreateWorkflowDefinition("Parallel Flow", parallel, CreateStep("Merge result", "Action"));
        var roundTripped = await RoundTripDefinitionAsync(definition);

        var steps = GetProperty(roundTripped, "Steps") as IList;
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(2);

        var branches = GetProperty(steps[0], "Steps") as IList;
        branches.Should().NotBeNull();
        branches!.Count.Should().Be(2);
        GetProperty(branches[0], "Name")?.ToString().Should().Be("Branch A");
        GetProperty(branches[1], "Name")?.ToString().Should().Be("Branch B");
        GetProperty(steps[1], "Name")?.ToString().Should().Be("Merge result");
    }

    [Fact]
    public async Task LoadDefinitionIntoCanvas_RoundTrips_TenTopLevelSteps_InOrder()
    {
        var definition = CreateWorkflowDefinition(
            "Ordered Flow",
            Enumerable.Range(1, 10).Select(index => CreateStep($"Step {index}", "Action")).ToArray());

        var roundTripped = await RoundTripDefinitionAsync(definition);

        var steps = GetProperty(roundTripped, "Steps") as IList;
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(10);
        steps.Cast<object>()
            .Select(step => GetProperty(step, "Name")?.ToString())
            .Should()
            .ContainInOrder(Enumerable.Range(1, 10).Select(index => $"Step {index}"));
    }

    [Fact]
    public void CanvasToDefinition_ParsesFractionalTimingValues_WithInvariantCulture()
    {
        var designer = Activator.CreateInstance(WorkflowDesignerType);
        designer.Should().NotBeNull();

        WorkflowDesignerType.GetField("_workflowName", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(designer, "Timing Flow");

        using var document = JsonDocument.Parse("""
            {
              "nodes": [
                {
                  "id": "node_1",
                  "type": "Delay",
                  "label": "Wait",
                  "config": {
                    "label": "Wait",
                    "delaySeconds": "0.5",
                    "timeoutSeconds": "1.5"
                  }
                }
              ],
              "edges": []
            }
            """);

        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

            var canvasToDefinition = WorkflowDesignerType.GetMethod("CanvasToDefinition", BindingFlags.Instance | BindingFlags.NonPublic);
            canvasToDefinition.Should().NotBeNull();

            var definition = canvasToDefinition!.Invoke(designer, [document.RootElement]);
            definition.Should().NotBeNull();

            var steps = GetProperty(definition!, "Steps") as IList;
            steps.Should().NotBeNull();
            steps!.Count.Should().Be(1);
            GetProperty(steps[0], "DelaySeconds").Should().Be(0.5d);
            GetProperty(steps[0], "TimeoutSeconds").Should().Be(1.5d);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void CanvasToDefinition_UsesSourceHandleContinue_ForRetrySibling()
    {
        var designer = Activator.CreateInstance(WorkflowDesignerType);
        designer.Should().NotBeNull();

        WorkflowDesignerType.GetField("_workflowName", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(designer, "Retry Raw Canvas");

        using var document = JsonDocument.Parse("""
            {
              "nodes": [
                { "id": "node_1", "type": "Retry", "label": "Retry" },
                { "id": "node_2", "type": "Action", "label": "Attempt" },
                { "id": "node_3", "type": "Action", "label": "Escalate" }
              ],
              "edges": [
                { "id": "edge_1", "source": "node_1", "target": "node_2", "sourceHandle": "body" },
                { "id": "edge_2", "source": "node_1", "target": "node_3", "sourceHandle": "continue" }
              ]
            }
            """);

        var canvasToDefinition = WorkflowDesignerType.GetMethod("CanvasToDefinition", BindingFlags.Instance | BindingFlags.NonPublic);
        canvasToDefinition.Should().NotBeNull();
        var definition = canvasToDefinition!.Invoke(designer, [document.RootElement]);
        definition.Should().NotBeNull();

        var steps = GetProperty(definition!, "Steps") as IList;
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(2);

        var retryChildren = GetProperty(steps[0], "Steps") as IList;
        retryChildren.Should().NotBeNull();
        retryChildren!.Count.Should().Be(1);
        GetProperty(retryChildren[0], "Name")?.ToString().Should().Be("Attempt");
        GetProperty(steps[1], "Name")?.ToString().Should().Be("Escalate");
    }

    [Fact]
    public void CanvasToDefinition_UsesSourceHandleInnerAndContinue_ForTimeout()
    {
        var designer = Activator.CreateInstance(WorkflowDesignerType);
        designer.Should().NotBeNull();

        WorkflowDesignerType.GetField("_workflowName", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(designer, "Timeout Raw Canvas");

        using var document = JsonDocument.Parse("""
            {
              "nodes": [
                { "id": "node_1", "type": "Timeout", "label": "Timeout", "config": { "timeoutSeconds": "30" } },
                { "id": "node_2", "type": "Action", "label": "Inner step" },
                { "id": "node_3", "type": "Action", "label": "After timeout" }
              ],
              "edges": [
                { "id": "edge_1", "source": "node_1", "target": "node_2", "sourceHandle": "inner" },
                { "id": "edge_2", "source": "node_1", "target": "node_3", "sourceHandle": "continue" }
              ]
            }
            """);

        var canvasToDefinition = WorkflowDesignerType.GetMethod("CanvasToDefinition", BindingFlags.Instance | BindingFlags.NonPublic);
        canvasToDefinition.Should().NotBeNull();
        var definition = canvasToDefinition!.Invoke(designer, [document.RootElement]);
        definition.Should().NotBeNull();

        var steps = GetProperty(definition!, "Steps") as IList;
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(2);
        GetProperty(GetProperty(steps[0], "Inner")!, "Name")?.ToString().Should().Be("Inner step");
        GetProperty(steps[1], "Name")?.ToString().Should().Be("After timeout");
    }

    [Fact]
    public void CanvasToDefinition_UsesSourceHandleTryFinallyAndContinue_ForTryCatch()
    {
        var designer = Activator.CreateInstance(WorkflowDesignerType);
        designer.Should().NotBeNull();

        WorkflowDesignerType.GetField("_workflowName", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(designer, "TryCatch Raw Canvas");

        using var document = JsonDocument.Parse("""
            {
              "nodes": [
                { "id": "node_1", "type": "TryCatch", "label": "TryCatch" },
                { "id": "node_2", "type": "Action", "label": "Do work" },
                { "id": "node_3", "type": "Action", "label": "Cleanup" },
                { "id": "node_4", "type": "Action", "label": "After try" }
              ],
              "edges": [
                { "id": "edge_1", "source": "node_1", "target": "node_2", "sourceHandle": "try" },
                { "id": "edge_2", "source": "node_1", "target": "node_3", "sourceHandle": "finally" },
                { "id": "edge_3", "source": "node_1", "target": "node_4", "sourceHandle": "continue" }
              ]
            }
            """);

        var canvasToDefinition = WorkflowDesignerType.GetMethod("CanvasToDefinition", BindingFlags.Instance | BindingFlags.NonPublic);
        canvasToDefinition.Should().NotBeNull();
        var definition = canvasToDefinition!.Invoke(designer, [document.RootElement]);
        definition.Should().NotBeNull();

        var steps = GetProperty(definition!, "Steps") as IList;
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(2);

        var tryBody = GetProperty(steps[0], "TryBody") as IList;
        var finallyBody = GetProperty(steps[0], "FinallyBody") as IList;
        tryBody.Should().NotBeNull();
        finallyBody.Should().NotBeNull();
        tryBody!.Count.Should().Be(1);
        finallyBody!.Count.Should().Be(1);
        GetProperty(tryBody[0], "Name")?.ToString().Should().Be("Do work");
        GetProperty(finallyBody[0], "Name")?.ToString().Should().Be("Cleanup");
        GetProperty(steps[1], "Name")?.ToString().Should().Be("After try");
    }

    [Fact]
    public void CanvasToDefinition_DoesNotDrop_RealNode_WithSyntheticLookingLabel()
    {
        var designer = Activator.CreateInstance(WorkflowDesignerType);
        designer.Should().NotBeNull();

        WorkflowDesignerType.GetField("_workflowName", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(designer, "Label Collision Flow");

        using var document = JsonDocument.Parse("""
            {
              "nodes": [
                {
                  "id": "node_1",
                  "type": "Action",
                  "label": "↪ Continue",
                  "config": {
                    "label": "↪ Continue",
                    "expression": "true"
                  }
                }
              ],
              "edges": []
            }
            """);

        var canvasToDefinition = WorkflowDesignerType.GetMethod("CanvasToDefinition", BindingFlags.Instance | BindingFlags.NonPublic);
        canvasToDefinition.Should().NotBeNull();
        var definition = canvasToDefinition!.Invoke(designer, [document.RootElement]);
        definition.Should().NotBeNull();

        var steps = GetProperty(definition!, "Steps") as IList;
        steps.Should().NotBeNull();
        steps!.Count.Should().Be(1);
        GetProperty(steps[0], "Name")?.ToString().Should().Be("↪ Continue");
    }

    [Fact]
    public void BuildCanvasGraph_AssignsStableParallelBranchKinds()
    {
        var parallel = CreateStep("Fan out", "Parallel");
        SetProperty(parallel, "Steps", CreateList("Steps",
            CreateStep("Branch 1", "Action"),
            CreateStep("Branch 2", "Action"),
            CreateStep("Branch 3", "Action"),
            CreateStep("Branch 4", "Action")));

        var definition = CreateWorkflowDefinition("Parallel Flow", parallel);

        var buildCanvasGraph = WorkflowDesignerType.GetMethod("BuildCanvasGraph", BindingFlags.Static | BindingFlags.NonPublic);
        buildCanvasGraph.Should().NotBeNull();
        var canvas = buildCanvasGraph!.Invoke(null, [definition]);
        canvas.Should().NotBeNull();

        var edges = GetProperty(canvas!, "Edges") as IList;
        edges.Should().NotBeNull();

        edges!.Cast<object>()
            .Select(edge => GetProperty(edge, "Kind")?.ToString())
            .Where(kind => kind is not null && kind.StartsWith("output-", StringComparison.Ordinal))
            .Should()
            .ContainInOrder("output-1", "output-2", "output-3", "output-4");
    }

    private static object CreateWorkflowDefinition(string name, params object[] steps)
    {
        var workflowDefinitionType = FindTypeByName("WorkflowDefinitionDto");
        var definition = Activator.CreateInstance(workflowDefinitionType)!;
        workflowDefinitionType.GetProperty("Name")!.SetValue(definition, name);

        var stepsList = workflowDefinitionType.GetProperty("Steps")!.GetValue(definition) as IList;
        stepsList.Should().NotBeNull();
        foreach (var step in steps)
            stepsList!.Add(step);

        return definition;
    }

    private static object CreateStep(string name, string type)
    {
        var stepType = FindTypeByName("StepDefinitionApiDto");
        var step = Activator.CreateInstance(stepType)!;
        stepType.GetProperty("Name")!.SetValue(step, name);
        stepType.GetProperty("Type")!.SetValue(step, type);
        return step;
    }

    private static void SetProperty(object target, string name, object? value)
        => target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);

    private static object CreateList(string propertyName, params object[] items)
    {
        var stepType = FindTypeByName("StepDefinitionApiDto");
        var listType = typeof(List<>).MakeGenericType(stepType);
        var list = Activator.CreateInstance(listType) as IList;
        list.Should().NotBeNull();
        foreach (var item in items)
            list!.Add(item);
        return list!;
    }

    private static async Task<object> RoundTripDefinitionAsync(object definition)
    {
        var designer = Activator.CreateInstance(WorkflowDesignerType);
        designer.Should().NotBeNull();

        WorkflowDesignerType.GetField("_workflowName", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(designer, GetProperty(definition, "Name")?.ToString());

        var buildCanvasGraph = WorkflowDesignerType.GetMethod("BuildCanvasGraph", BindingFlags.Static | BindingFlags.NonPublic);
        buildCanvasGraph.Should().NotBeNull();
        var canvas = buildCanvasGraph!.Invoke(null, [definition]);
        canvas.Should().NotBeNull();

        var browserShape = new
        {
            nodes = GetProperty(canvas!, "Nodes"),
            edges = ((GetProperty(canvas!, "Edges") as IEnumerable) ?? Array.Empty<object>())
                .Cast<object>()
                .Select(edge => new
                {
                    id = GetProperty(edge, "Id"),
                    source = GetProperty(edge, "Source"),
                    target = GetProperty(edge, "Target"),
                    sourceHandle = GetProperty(edge, "Kind"),
                    label = GetProperty(edge, "Label")
                })
                .ToArray()
        };

        var canvasJson = JsonSerializer.Serialize(browserShape);
        using var canvasDocument = JsonDocument.Parse(canvasJson);

        var canvasToDefinition = WorkflowDesignerType.GetMethod("CanvasToDefinition", BindingFlags.Instance | BindingFlags.NonPublic);
        canvasToDefinition.Should().NotBeNull();
        var roundTripped = canvasToDefinition!.Invoke(designer, [canvasDocument.RootElement]);
        roundTripped.Should().NotBeNull();
        return await Task.FromResult(roundTripped!);
    }

    private static object? GetProperty(object target, string name)
        => target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target);

    private static void AddVariable(object list, Type variableType, string token)
    {
        var item = Activator.CreateInstance(variableType);
        item.Should().NotBeNull();
        variableType.GetProperty("Token")!.SetValue(item, token);
        variableType.GetProperty("GroupLabel")!.SetValue(item, "Available outputs");
        list.GetType().GetMethod("Add")!.Invoke(list, [item]);
    }

    private static Assembly LoadWebAssembly()
        => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "WorkflowFramework.Dashboard.Web")
            ?? Assembly.Load("WorkflowFramework.Dashboard.Web");

    private static Type FindTypeByName(string typeName)
        => WebAssembly.GetTypes().First(t => string.Equals(t.Name, typeName, StringComparison.Ordinal));
}
#endif
