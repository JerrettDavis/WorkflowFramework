using System.Collections;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace WorkflowFramework.Dashboard.Tests;

#if NET10_0
public sealed class WorkflowDesignerInteropContractTests
{
    private static readonly Assembly WebAssembly = LoadWebAssembly();
    private static readonly Type WorkflowDesignerType = FindTypeByName("WorkflowDesigner");

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

    private static object? GetProperty(object target, string name)
        => target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target);

    private static Assembly LoadWebAssembly()
        => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "WorkflowFramework.Dashboard.Web")
            ?? Assembly.Load("WorkflowFramework.Dashboard.Web");

    private static Type FindTypeByName(string typeName)
        => WebAssembly.GetTypes().First(t => string.Equals(t.Name, typeName, StringComparison.Ordinal));
}
#endif
