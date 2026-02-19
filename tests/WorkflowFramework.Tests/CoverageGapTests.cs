using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.Agents.Mcp;
using WorkflowFramework.Extensions.Agents.Skills;
using WorkflowFramework.Extensions.Connectors.Messaging;
using WorkflowFramework.Extensions.DataMapping.Readers;
using WorkflowFramework.Extensions.DataMapping.Writers;
using WorkflowFramework.Extensions.Integration.Composition;
using WorkflowFramework.Extensions.Plugins;
using WorkflowFramework.Testing;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests;

#region CommandHook Internal Methods

public class CommandHookSerializationTests
{
    [Fact]
    public async Task ExecuteAsync_InvalidCommand_ReturnsDeny()
    {
        var hook = new CommandHook("nonexistent_command_xyz_12345");
        var ctx = new HookContext { StepName = "s", ToolName = "t" };
        // This should either fail to start the process or exit with non-zero code
        try
        {
            var result = await hook.ExecuteAsync(AgentHookEvent.PreToolCall, ctx);
            // If it returns, it should be a deny result
            result.Decision.Should().Be(HookDecision.Deny);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Expected on some platforms when command doesn't exist
        }
    }

    [Fact]
    public void Timeout_SetterWorks()
    {
        var hook = new CommandHook("cmd");
        hook.Timeout = TimeSpan.FromMinutes(5);
        hook.Timeout.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Constructor_WithArgs()
    {
        var hook = new CommandHook("cmd", args: new[] { "--flag", "val" }, matcher: "test*", timeout: TimeSpan.FromSeconds(10));
        hook.Matcher.Should().Be("test*");
        hook.Timeout.Should().Be(TimeSpan.FromSeconds(10));
    }
}

#endregion

#region ObjectSourceReader Edge Cases

public class ObjectSourceReaderExtendedTests
{
    private readonly ObjectSourceReader _reader = new();

    [Fact]
    public void SupportedPrefixes_ContainsAtDot()
    {
        _reader.SupportedPrefixes.Should().Contain("@.");
    }

    [Fact]
    public void CanRead_ValidPath_ReturnsTrue()
    {
        _reader.CanRead("@.Name").Should().BeTrue();
    }

    [Fact]
    public void CanRead_InvalidPath_ReturnsFalse()
    {
        _reader.CanRead("$.Name").Should().BeFalse();
        _reader.CanRead("Name").Should().BeFalse();
    }

    [Fact]
    public void Read_NullPath_ReturnsNull()
    {
        _reader.Read(null!, new object()).Should().BeNull();
    }

    [Fact]
    public void Read_EmptyPath_ReturnsNull()
    {
        _reader.Read("", new object()).Should().BeNull();
    }

    [Fact]
    public void Read_InvalidPrefix_ReturnsNull()
    {
        _reader.Read("$.Name", new object()).Should().BeNull();
    }

    [Fact]
    public void Read_NonExistentProperty_ReturnsNull()
    {
        var obj = new { Name = "Alice" };
        _reader.Read("@.Missing", obj).Should().BeNull();
    }

    [Fact]
    public void Read_NestedProperty()
    {
        var obj = new { User = new { Name = "Alice" } };
        _reader.Read("@.User.Name", obj).Should().Be("Alice");
    }

    [Fact]
    public void Read_NullNestedProperty_ReturnsNull()
    {
        var obj = new NullableNested();
        _reader.Read("@.Inner.Name", obj).Should().BeNull();
    }

    [Fact]
    public void Read_NullPropertyValue_ReturnsNull()
    {
        var obj = new NullableNested { Inner = new InnerObj { Name = null! } };
        _reader.Read("@.Inner.Name", obj).Should().BeNull();
    }

    [Fact]
    public void Read_SimpleProperty()
    {
        var obj = new { Count = 42 };
        _reader.Read("@.Count", obj).Should().Be("42");
    }

    private class NullableNested
    {
        public InnerObj? Inner { get; set; }
    }

    private class InnerObj
    {
        public string Name { get; set; } = "";
    }
}

#endregion

#region XmlSourceReader Edge Cases

public class XmlSourceReaderExtendedTests
{
    private readonly XmlSourceReader _reader = new();

    [Fact]
    public void SupportedPrefixes_ContainsSlash()
    {
        _reader.SupportedPrefixes.Should().Contain("/");
    }

    [Fact]
    public void CanRead_ValidPath_ReturnsTrue()
    {
        _reader.CanRead("/root/name").Should().BeTrue();
        _reader.CanRead("//name").Should().BeTrue();
    }

    [Fact]
    public void CanRead_InvalidPath_ReturnsFalse()
    {
        _reader.CanRead("$.name").Should().BeFalse();
    }

    [Fact]
    public void Read_NullPath_ReturnsNull()
    {
        var doc = new XDocument(new XElement("root"));
        _reader.Read(null!, doc).Should().BeNull();
    }

    [Fact]
    public void Read_EmptyPath_ReturnsNull()
    {
        var doc = new XDocument(new XElement("root"));
        _reader.Read("", doc).Should().BeNull();
    }

    [Fact]
    public void Read_XPathToElement()
    {
        var doc = XDocument.Parse("<root><name>Alice</name></root>");
        _reader.Read("/root/name", doc).Should().Be("Alice");
    }

    [Fact]
    public void Read_XPathToAttribute()
    {
        var doc = XDocument.Parse("<root id='42'/>");
        _reader.Read("/root/@id", doc).Should().Be("42");
    }

    [Fact]
    public void Read_XPathNoMatch_ReturnsNull()
    {
        var doc = XDocument.Parse("<root/>");
        _reader.Read("/root/missing", doc).Should().BeNull();
    }

    [Fact]
    public void Read_XPathTextNode()
    {
        var doc = XDocument.Parse("<root>hello</root>");
        // XPath /root/text() returns XText
        _reader.Read("/root/text()", doc).Should().Be("hello");
    }

    [Fact]
    public void Read_XPathNumericResult()
    {
        var doc = XDocument.Parse("<root><x>5</x><y>3</y></root>");
        _reader.Read("count(/root/*)", doc).Should().NotBeNull();
    }
}

#endregion

#region DictionarySourceReader Edge Cases

public class DictionarySourceReaderExtendedTests
{
    private readonly DictionarySourceReader _reader = new();

    [Fact]
    public void SupportedPrefixes_ContainsEmpty()
    {
        _reader.SupportedPrefixes.Should().Contain(string.Empty);
    }

    [Fact]
    public void CanRead_ValidPath_ReturnsTrue()
    {
        _reader.CanRead("name").Should().BeTrue();
    }

    [Fact]
    public void CanRead_JsonPath_ReturnsFalse()
    {
        _reader.CanRead("$.name").Should().BeFalse();
    }

    [Fact]
    public void CanRead_XmlPath_ReturnsFalse()
    {
        _reader.CanRead("/root/name").Should().BeFalse();
    }

    [Fact]
    public void Read_EmptyPath_ReturnsNull()
    {
        var dict = new Dictionary<string, object?> { ["name"] = "Alice" };
        _reader.Read("", dict).Should().BeNull();
    }

    [Fact]
    public void Read_NullPath_ReturnsNull()
    {
        var dict = new Dictionary<string, object?>();
        _reader.Read(null!, dict).Should().BeNull();
    }

    [Fact]
    public void Read_MissingKey_ReturnsNull()
    {
        var dict = new Dictionary<string, object?> { ["name"] = "Alice" };
        _reader.Read("missing", dict).Should().BeNull();
    }

    [Fact]
    public void Read_NestedDictionary()
    {
        var dict = new Dictionary<string, object?>
        {
            ["user"] = new Dictionary<string, object?> { ["name"] = "Bob" }
        };
        _reader.Read("user.name", dict).Should().Be("Bob");
    }

    [Fact]
    public void Read_NestedNonDict_ReturnsNull()
    {
        var dict = new Dictionary<string, object?> { ["user"] = "not a dict" };
        _reader.Read("user.name", dict).Should().BeNull();
    }

    [Fact]
    public void Read_NullValue_ReturnsNull()
    {
        var dict = new Dictionary<string, object?> { ["key"] = null };
        _reader.Read("key", dict).Should().BeNull();
    }
}

#endregion

#region JsonSourceReader Edge Cases

public class JsonSourceReaderExtendedTests
{
    private readonly JsonSourceReader _reader = new();

    [Fact]
    public void SupportedPrefixes_ContainsDollarDot()
    {
        _reader.SupportedPrefixes.Should().Contain("$.");
    }

    [Fact]
    public void Read_InvalidPrefix_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("{}");
        _reader.Read("name", doc.RootElement).Should().BeNull();
    }

    [Fact]
    public void Read_NullPath_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("{}");
        _reader.Read(null!, doc.RootElement).Should().BeNull();
    }

    [Fact]
    public void Read_EmptyPath_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("{}");
        _reader.Read("", doc.RootElement).Should().BeNull();
    }

    [Fact]
    public void Read_BooleanTrue()
    {
        using var doc = JsonDocument.Parse("{\"flag\":true}");
        _reader.Read("$.flag", doc.RootElement).Should().Be("true");
    }

    [Fact]
    public void Read_BooleanFalse()
    {
        using var doc = JsonDocument.Parse("{\"flag\":false}");
        _reader.Read("$.flag", doc.RootElement).Should().Be("false");
    }

    [Fact]
    public void Read_NullValue()
    {
        using var doc = JsonDocument.Parse("{\"x\":null}");
        _reader.Read("$.x", doc.RootElement).Should().BeNull();
    }

    [Fact]
    public void Read_Number()
    {
        using var doc = JsonDocument.Parse("{\"x\":42}");
        _reader.Read("$.x", doc.RootElement).Should().Be("42");
    }

    [Fact]
    public void Read_ArrayIndex()
    {
        using var doc = JsonDocument.Parse("{\"items\":[\"a\",\"b\",\"c\"]}");
        _reader.Read("$.items[1]", doc.RootElement).Should().Be("b");
    }

    [Fact]
    public void Read_ArrayIndexOutOfRange_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("{\"items\":[\"a\"]}");
        _reader.Read("$.items[5]", doc.RootElement).Should().BeNull();
    }

    [Fact]
    public void Read_NestedObject()
    {
        using var doc = JsonDocument.Parse("{\"user\":{\"name\":\"Alice\"}}");
        _reader.Read("$.user.name", doc.RootElement).Should().Be("Alice");
    }

    [Fact]
    public void Read_MissingProperty_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("{\"x\":1}");
        _reader.Read("$.missing", doc.RootElement).Should().BeNull();
    }

    [Fact]
    public void Read_ObjectResult_ReturnsRawText()
    {
        using var doc = JsonDocument.Parse("{\"nested\":{\"a\":1}}");
        _reader.Read("$.nested", doc.RootElement).Should().Contain("\"a\"");
    }

    [Fact]
    public void Read_ArrayPropertyNotArray_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("{\"items\":\"notarray\"}");
        _reader.Read("$.items[0]", doc.RootElement).Should().BeNull();
    }
}

#endregion

#region ObjectDestinationWriter Edge Cases

public class ObjectDestinationWriterExtendedTests
{
    private readonly ObjectDestinationWriter _writer = new();

    [Fact]
    public void SupportedPrefixes_ContainsAtDot()
    {
        _writer.SupportedPrefixes.Should().Contain("@.");
    }

    [Fact]
    public void CanWrite_ValidPath_ReturnsTrue()
    {
        _writer.CanWrite("@.Name").Should().BeTrue();
    }

    [Fact]
    public void CanWrite_InvalidPath_ReturnsFalse()
    {
        _writer.CanWrite("$.Name").Should().BeFalse();
        _writer.CanWrite("Name").Should().BeFalse();
    }

    [Fact]
    public void Write_EmptyPath_ReturnsFalse()
    {
        _writer.Write("", "val", new TestObj()).Should().BeFalse();
    }

    [Fact]
    public void Write_NullPath_ReturnsFalse()
    {
        _writer.Write(null!, "val", new TestObj()).Should().BeFalse();
    }

    [Fact]
    public void Write_InvalidPrefix_ReturnsFalse()
    {
        _writer.Write("$.Name", "val", new TestObj()).Should().BeFalse();
    }

    [Fact]
    public void Write_NonExistentProperty_ReturnsFalse()
    {
        _writer.Write("@.Missing", "val", new TestObj()).Should().BeFalse();
    }

    [Fact]
    public void Write_ReadOnlyProperty_ReturnsFalse()
    {
        _writer.Write("@.ReadOnly", "val", new TestObj()).Should().BeFalse();
    }

    [Fact]
    public void Write_NullValue_SetsDefault()
    {
        var obj = new TestObj { Age = 42 };
        _writer.Write("@.Age", null, obj).Should().BeTrue();
        obj.Age.Should().Be(0);
    }

    [Fact]
    public void Write_NullValueToString_SetsNull()
    {
        var obj = new TestObj { Name = "old" };
        _writer.Write("@.Name", null, obj).Should().BeTrue();
        obj.Name.Should().BeNull();
    }

    [Fact]
    public void Write_NestedProperty()
    {
        var obj = new TestObj { Inner = new InnerTestObj() };
        _writer.Write("@.Inner.Value", "hello", obj).Should().BeTrue();
        obj.Inner.Value.Should().Be("hello");
    }

    [Fact]
    public void Write_NestedPropertyNullParent_ReturnsFalse()
    {
        var obj = new TestObj();
        _writer.Write("@.Inner.Value", "hello", obj).Should().BeFalse();
    }

    [Fact]
    public void Write_NullableIntProperty()
    {
        var obj = new TestObj();
        _writer.Write("@.NullableAge", "25", obj).Should().BeTrue();
        obj.NullableAge.Should().Be(25);
    }

    private class TestObj
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public int? NullableAge { get; set; }
        public string ReadOnly => "readonly";
        public InnerTestObj? Inner { get; set; }
    }

    private class InnerTestObj
    {
        public string Value { get; set; } = "";
    }
}

#endregion

#region XmlDestinationWriter Edge Cases

public class XmlDestinationWriterExtendedTests
{
    private readonly XmlDestinationWriter _writer = new();

    [Fact]
    public void SupportedPrefixes_ContainsSlash()
    {
        _writer.SupportedPrefixes.Should().Contain("/");
    }

    [Fact]
    public void CanWrite_ValidPath_ReturnsTrue()
    {
        _writer.CanWrite("/root/name").Should().BeTrue();
    }

    [Fact]
    public void CanWrite_InvalidPath_ReturnsFalse()
    {
        _writer.CanWrite("$.name").Should().BeFalse();
    }

    [Fact]
    public void Write_EmptyPath_ReturnsFalse()
    {
        var doc = new XDocument(new XElement("root"));
        _writer.Write("", "val", doc).Should().BeFalse();
    }

    [Fact]
    public void Write_NullPath_ReturnsFalse()
    {
        var doc = new XDocument(new XElement("root"));
        _writer.Write(null!, "val", doc).Should().BeFalse();
    }

    [Fact]
    public void Write_NoRoot_ReturnsFalse()
    {
        var doc = new XDocument();
        _writer.Write("/root/name", "val", doc).Should().BeFalse();
    }

    [Fact]
    public void Write_RootMismatch_ReturnsFalse()
    {
        var doc = new XDocument(new XElement("other"));
        _writer.Write("/root/name", "val", doc).Should().BeFalse();
    }

    [Fact]
    public void Write_NullValue_SetsEmpty()
    {
        var doc = new XDocument(new XElement("root"));
        _writer.Write("/root/name", null, doc).Should().BeTrue();
        doc.Root!.Element("name")!.Value.Should().Be(string.Empty);
    }

    [Fact]
    public void Write_RootOnly()
    {
        var doc = new XDocument(new XElement("root"));
        _writer.Write("/root", "hello", doc).Should().BeTrue();
        doc.Root!.Value.Should().Be("hello");
    }

    [Fact]
    public void Write_ExistingElement_Overwrites()
    {
        var doc = XDocument.Parse("<root><name>old</name></root>");
        _writer.Write("/root/name", "new", doc).Should().BeTrue();
        doc.Root!.Element("name")!.Value.Should().Be("new");
    }

    [Fact]
    public void Write_OnlySlash_ReturnsFalse()
    {
        var doc = new XDocument(new XElement("root"));
        _writer.Write("/", "val", doc).Should().BeFalse();
    }
}

#endregion

#region DictionaryDestinationWriter Edge Cases

public class DictionaryDestinationWriterExtendedTests
{
    private readonly DictionaryDestinationWriter _writer = new();

    [Fact]
    public void SupportedPrefixes_ContainsEmpty()
    {
        _writer.SupportedPrefixes.Should().Contain(string.Empty);
    }

    [Fact]
    public void CanWrite_ValidPath_ReturnsTrue()
    {
        _writer.CanWrite("name").Should().BeTrue();
    }

    [Fact]
    public void CanWrite_JsonPath_ReturnsFalse()
    {
        _writer.CanWrite("$.name").Should().BeFalse();
    }

    [Fact]
    public void CanWrite_XmlPath_ReturnsFalse()
    {
        _writer.CanWrite("/root/name").Should().BeFalse();
    }

    [Fact]
    public void CanWrite_EmptyPath_ReturnsFalse()
    {
        _writer.CanWrite("").Should().BeFalse();
    }

    [Fact]
    public void Write_EmptyPath_ReturnsFalse()
    {
        var dict = new Dictionary<string, object?>();
        _writer.Write("", "val", dict).Should().BeFalse();
    }

    [Fact]
    public void Write_NullValue()
    {
        var dict = new Dictionary<string, object?>();
        _writer.Write("key", null, dict).Should().BeTrue();
        dict["key"].Should().BeNull();
    }

    [Fact]
    public void Write_OverwriteExistingNested()
    {
        var dict = new Dictionary<string, object?> { ["a"] = "not a dict" };
        // Should create new nested dict overwriting the string
        _writer.Write("a.b", "val", dict).Should().BeTrue();
        ((IDictionary<string, object?>)dict["a"]!)["b"].Should().Be("val");
    }
}

#endregion

#region JsonDestinationWriter Edge Cases

public class JsonDestinationWriterExtendedTests
{
    private readonly JsonDestinationWriter _writer = new();

    [Fact]
    public void SupportedPrefixes_ContainsDollarDot()
    {
        _writer.SupportedPrefixes.Should().Contain("$.");
    }

    [Fact]
    public void CanWrite_ValidPath_ReturnsTrue()
    {
        _writer.CanWrite("$.name").Should().BeTrue();
    }

    [Fact]
    public void CanWrite_InvalidPath_ReturnsFalse()
    {
        _writer.CanWrite("name").Should().BeFalse();
    }

    [Fact]
    public void Write_EmptyPath_ReturnsFalse()
    {
        var obj = new JsonObject();
        _writer.Write("", "val", obj).Should().BeFalse();
    }

    [Fact]
    public void Write_InvalidPrefix_ReturnsFalse()
    {
        var obj = new JsonObject();
        _writer.Write("name", "val", obj).Should().BeFalse();
    }

    [Fact]
    public void Write_NullValue()
    {
        var obj = new JsonObject();
        _writer.Write("$.x", null, obj).Should().BeTrue();
    }

    [Fact]
    public void Write_OverwriteExistingNested()
    {
        var obj = new JsonObject { ["a"] = JsonValue.Create("str") };
        _writer.Write("$.a.b", "val", obj).Should().BeTrue();
    }
}

#endregion

#region WorkflowTestHarness

public class WorkflowTestHarnessExtendedTests
{
    [Fact]
    public async Task ExecuteAsync_WithoutOverrides_RunsOriginal()
    {
        var harness = new WorkflowTestHarness();
        var step = new TrackingStep("S1");
        var wf = Workflow.Create("test").Step(step).Build();
        var ctx = new WorkflowContext();
        var result = await harness.ExecuteAsync(wf, ctx);
        result.Status.Should().Be(WorkflowStatus.Completed);
        TrackingStep.GetLog(ctx).Should().Contain("S1");
    }

    [Fact]
    public async Task ExecuteAsync_WithOverride_ReplacesStep()
    {
        var harness = new WorkflowTestHarness();
        harness.OverrideStep("S1", ctx => { ctx.Properties["replaced"] = true; return Task.CompletedTask; });
        var step = new TrackingStep("S1");
        var wf = Workflow.Create("test").Step(step).Build();
        var ctx = new WorkflowContext();
        await harness.ExecuteAsync(wf, ctx);
        ctx.Properties["replaced"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_Typed_WithoutOverrides_RunsOriginal()
    {
        var harness = new WorkflowTestHarness();
        var step = new TypedTrackingStep<TestData>("S1");
        var wf = Workflow.Create<TestData>("test").Step(step).Build();
        var result = await harness.ExecuteAsync(wf, new TestData());
        result.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_Typed_WithOverrides_FallsThroughWhenNotIWorkflow()
    {
        // IWorkflow<TData> doesn't extend IWorkflow, so overrides can't apply
        // — the harness falls through and runs the original steps
        var harness = new WorkflowTestHarness();
        var fake = new FakeStep("S1", ctx => { ctx.Properties["replaced"] = true; return Task.CompletedTask; });
        harness.OverrideStep("S1", fake);
        var step = new TypedTrackingStep<TestData>("S1");
        var wf = Workflow.Create<TestData>("test").Step(step).Build();
        var result = await harness.ExecuteAsync(wf, new TestData());
        result.Status.Should().Be(WorkflowStatus.Completed);
        // Override not applied — typed workflow doesn't implement IWorkflow
        fake.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task OverrideStep_Chaining()
    {
        var harness = new WorkflowTestHarness()
            .OverrideStep("A", new FakeStep("A"))
            .OverrideStep("B", new FakeStep("B"));
        var wf = Workflow.Create("test")
            .Step(new TrackingStep("A"))
            .Step(new TrackingStep("B"))
            .Build();
        var result = await harness.ExecuteAsync(wf, new WorkflowContext());
        result.Status.Should().Be(WorkflowStatus.Completed);
    }

    private class TestData
    {
        public string Value { get; set; } = "";
    }
}

#endregion

#region FakeStep<T>

public class FakeStepTypedCoverageTests
{
    [Fact]
    public async Task ExecuteAsync_NoAction_NoOp()
    {
        var step = new FakeStep<TestData>("test");
        step.Name.Should().Be("test");
        step.ExecutionCount.Should().Be(0);
        var ctx = new WorkflowContext<TestData>(new TestData(), CancellationToken.None);
        await step.ExecuteAsync(ctx);
        step.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithAction_ExecutesIt()
    {
        var called = false;
        var step = new FakeStep<TestData>("test", ctx => { called = true; return Task.CompletedTask; });
        var ctx = new WorkflowContext<TestData>(new TestData(), CancellationToken.None);
        await step.ExecuteAsync(ctx);
        called.Should().BeTrue();
        step.ExecutionCount.Should().Be(1);
    }

    private class TestData
    {
        public string Value { get; set; } = "";
    }
}

#endregion

#region WorkflowPluginContext

public class WorkflowPluginContextExtendedTests
{
    [Fact]
    public void RegisterStep_AddsToServices()
    {
        var services = new ServiceCollection();
        var ctx = new WorkflowPluginContext(services);
        ctx.RegisterStep<TestStep>();
        services.Should().Contain(sd => sd.ServiceType == typeof(TestStep));
    }

    [Fact]
    public void RegisterMiddleware_AddsToServices()
    {
        var services = new ServiceCollection();
        var ctx = new WorkflowPluginContext(services);
        ctx.RegisterMiddleware<TestMiddleware>();
        services.Should().Contain(sd => sd.ServiceType == typeof(IWorkflowMiddleware));
    }

    [Fact]
    public void RegisterEvents_AddsToServices()
    {
        var services = new ServiceCollection();
        var ctx = new WorkflowPluginContext(services);
        ctx.RegisterEvents<TestEvents>();
        services.Should().Contain(sd => sd.ServiceType == typeof(IWorkflowEvents));
    }

    [Fact]
    public void OnEvent_GetEventHooks_ReturnsRegisteredHooks()
    {
        var services = new ServiceCollection();
        var ctx = new WorkflowPluginContext(services);
        var called = false;
        ctx.OnEvent("test", _ => { called = true; return Task.CompletedTask; });
        var hooks = ctx.GetEventHooks("test");
        hooks.Should().HaveCount(1);
    }

    [Fact]
    public void GetEventHooks_UnknownEvent_ReturnsEmpty()
    {
        var services = new ServiceCollection();
        var ctx = new WorkflowPluginContext(services);
        ctx.GetEventHooks("unknown").Should().BeEmpty();
    }

    [Fact]
    public void Constructor_NullServices_Throws()
    {
        var act = () => new WorkflowPluginContext(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Services_ReturnsProvidedCollection()
    {
        var services = new ServiceCollection();
        var ctx = new WorkflowPluginContext(services);
        ctx.Services.Should().BeSameAs(services);
    }

    private class TestStep : IStep
    {
        public string Name => "test";
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    private class TestMiddleware : IWorkflowMiddleware
    {
        public Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next) => next(context);
    }

    private class TestEvents : WorkflowEventsBase { }
}

#endregion

#region ScatterGatherStep Extended

public class ScatterGatherStepExtendedTests
{
    [Fact]
    public async Task ExecuteAsync_AllHandlersComplete()
    {
        var h1 = new ResultStep("H1", "result1");
        var h2 = new ResultStep("H2", "result2");
        object?[]? gathered = null;
        var step = new ScatterGatherStep(
            new IStep[] { h1, h2 },
            (results, ctx) => { gathered = results.ToArray(); return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        step.Name.Should().Be("ScatterGather");
        var ctx = new WorkflowContext();
        await step.ExecuteAsync(ctx);
        ctx.Properties.Should().ContainKey(ScatterGatherStep.ResultsKey);
    }

    [Fact]
    public async Task ExecuteAsync_HandlerThrows_ReturnsNull()
    {
        var h1 = new ThrowingStep("H1");
        var h2 = new ResultStep("H2", "ok");
        var step = new ScatterGatherStep(
            new IStep[] { h1, h2 },
            (results, _) => Task.CompletedTask,
            TimeSpan.FromSeconds(5));

        var ctx = new WorkflowContext();
        await step.ExecuteAsync(ctx);
        var results = (object?[])ctx.Properties[ScatterGatherStep.ResultsKey]!;
        results.Should().HaveCount(2);
        results[0].Should().BeNull(); // failed handler
    }

    [Fact]
    public void Constructor_NullHandlers_Throws()
    {
        var act = () => new ScatterGatherStep(null!, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullAggregator_Throws()
    {
        var act = () => new ScatterGatherStep(Array.Empty<IStep>(), null!, TimeSpan.FromSeconds(1));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_EmptyHandlers_AggregatesEmpty()
    {
        object?[]? gathered = null;
        var step = new ScatterGatherStep(
            Array.Empty<IStep>(),
            (results, ctx) => { gathered = results.ToArray(); return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        var ctx = new WorkflowContext();
        await step.ExecuteAsync(ctx);
        gathered.Should().BeEmpty();
    }

    private class ThrowingStep(string name) : IStep
    {
        public string Name => name;
        public Task ExecuteAsync(IWorkflowContext context) => throw new InvalidOperationException("boom");
    }

    private class ResultStep(string name, string result) : IStep
    {
        public string Name => name;
        public Task ExecuteAsync(IWorkflowContext context)
        {
            context.Properties[$"__Result_{Name}"] = result;
            return Task.CompletedTask;
        }
    }

    private class SlowStep(string name, TimeSpan delay) : IStep
    {
        public string Name => name;
        public async Task ExecuteAsync(IWorkflowContext context)
        {
            await Task.Delay(delay, context.CancellationToken);
        }
    }
}

#endregion

#region AgentBuilderExtensions & AgentToolingOptions

public class AgentBuilderExtensionsTests
{
    [Fact]
    public void AgentLoop_AddsStep()
    {
        var provider = new WorkflowFramework.Extensions.AI.EchoAgentProvider();
        var registry = new ToolRegistry();
        var builder = Workflow.Create("test");
        builder.AgentLoop(provider, registry, opts =>
        {
            opts.MaxIterations = 5;
        });
        var wf = builder.Build();
        wf.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void CallTool_AddsStep()
    {
        var registry = new ToolRegistry();
        var builder = Workflow.Create("test");
        builder.CallTool(registry, "myTool", "{}", "customStep");
        var wf = builder.Build();
        wf.Steps.Should().HaveCount(1);
    }
}

public class AgentToolingOptionsTests
{
    [Fact]
    public void Defaults()
    {
        var opts = new AgentToolingOptions();
        opts.MaxToolConcurrency.Should().Be(4);
        opts.DefaultTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void SetValues()
    {
        var opts = new AgentToolingOptions
        {
            MaxToolConcurrency = 8,
            DefaultTimeout = TimeSpan.FromMinutes(1)
        };
        opts.MaxToolConcurrency.Should().Be(8);
        opts.DefaultTimeout.Should().Be(TimeSpan.FromMinutes(1));
    }
}

#endregion

#region MCP Builder & Config & ServiceCollection

public class McpBuilderExtensionsTests
{
    [Fact]
    public void CallMcpTool_WithRegistry_AddsStep()
    {
        var registry = new ToolRegistry();
        var builder = Workflow.Create("test");
        builder.CallMcpTool("server1", "tool1", "{}", registry);
        var wf = builder.Build();
        wf.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void CallMcpTool_WithoutRegistry_AddsStep()
    {
        var builder = Workflow.Create("test");
        builder.CallMcpTool("server1", "tool1", "{}");
        var wf = builder.Build();
        wf.Steps.Should().HaveCount(1);
    }
}

public class McpServerConfigTests
{
    [Fact]
    public void Defaults()
    {
        var config = new McpServerConfig();
        config.Name.Should().BeEmpty();
        config.Transport.Should().Be("stdio");
        config.Command.Should().BeNull();
        config.Args.Should().BeNull();
        config.Url.Should().BeNull();
        config.Headers.Should().BeNull();
        config.Env.Should().BeNull();
    }

    [Fact]
    public void SetValues()
    {
        var config = new McpServerConfig
        {
            Name = "test",
            Transport = "http",
            Url = "http://localhost",
            Headers = new Dictionary<string, string> { ["key"] = "val" },
            Command = "cmd",
            Args = new[] { "--arg" },
            Env = new Dictionary<string, string> { ["VAR"] = "val" }
        };
        config.Name.Should().Be("test");
        config.Transport.Should().Be("http");
        config.Url.Should().Be("http://localhost");
    }
}

public class McpServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMcpServer_NullConfig_Throws()
    {
        var services = new ServiceCollection();
        var act = () => services.AddMcpServer(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddMcpServer_StdioTransport_RegistersServices()
    {
        var services = new ServiceCollection();
        var config = new McpServerConfig
        {
            Name = "test",
            Transport = "stdio",
            Command = "echo"
        };
        services.AddMcpServer(config);
        services.Should().Contain(sd => sd.ServiceType == typeof(IToolProvider));
        services.Should().Contain(sd => sd.ServiceType == typeof(IContextSource));
    }

    [Fact]
    public void AddMcpServer_HttpTransport_RegistersServices()
    {
        var services = new ServiceCollection();
        var config = new McpServerConfig
        {
            Name = "test",
            Transport = "http",
            Url = "http://localhost:3000"
        };
        services.AddMcpServer(config);
        services.Should().Contain(sd => sd.ServiceType == typeof(IToolProvider));
    }

    [Fact]
    public void AddMcpServer_UnknownTransport_ThrowsOnResolve()
    {
        var services = new ServiceCollection();
        var config = new McpServerConfig
        {
            Name = "test",
            Transport = "unknown"
        };
        services.AddMcpServer(config);
        var sp = services.BuildServiceProvider();
        var act = () => sp.GetRequiredService<IToolProvider>();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddMcpServer_StdioMissingCommand_ThrowsOnResolve()
    {
        var services = new ServiceCollection();
        var config = new McpServerConfig { Name = "test", Transport = "stdio" };
        services.AddMcpServer(config);
        var sp = services.BuildServiceProvider();
        var act = () => sp.GetRequiredService<IToolProvider>();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddMcpServer_HttpMissingUrl_ThrowsOnResolve()
    {
        var services = new ServiceCollection();
        var config = new McpServerConfig { Name = "test", Transport = "http" };
        services.AddMcpServer(config);
        var sp = services.BuildServiceProvider();
        var act = () => sp.GetRequiredService<IToolProvider>();
        act.Should().Throw<InvalidOperationException>();
    }
}

#endregion

#region HttpMcpTransport

public class HttpMcpTransportTests
{
    [Fact]
    public void Constructor_NullUrl_Throws()
    {
        var act = () => new HttpMcpTransport(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ConnectAsync_ThenDisconnect()
    {
        using var transport = new HttpMcpTransport("http://localhost:12345");
        await transport.ConnectAsync();
        await transport.DisconnectAsync();
    }

    [Fact]
    public void ReceiveAsync_NoMessages_Throws()
    {
        using var transport = new HttpMcpTransport("http://localhost:12345");
        var act = async () => await transport.ReceiveAsync();
        act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendAsync_NotConnected_Throws()
    {
        using var transport = new HttpMcpTransport("http://localhost:12345");
        var msg = new McpJsonRpcMessage { Method = "test" };
        var act = async () => await transport.SendAsync(msg);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Dispose_MultipleCallsIsSafe()
    {
        var transport = new HttpMcpTransport("http://localhost:12345");
        transport.Dispose();
        transport.Dispose(); // should not throw
    }

    [Fact]
    public async Task ConnectAsync_WithHeaders()
    {
        var headers = new Dictionary<string, string> { ["Authorization"] = "Bearer token" };
        using var transport = new HttpMcpTransport("http://localhost:12345", headers);
        await transport.ConnectAsync();
        await transport.DisconnectAsync();
    }
}

#endregion

#region StdioMcpTransport Extended

public class StdioMcpTransportExtendedTests
{
    [Fact]
    public void Constructor_NullCommand_Throws()
    {
        var act = () => new StdioMcpTransport(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SendAsync_NotConnected_Throws()
    {
        using var transport = new StdioMcpTransport("echo");
        var msg = new McpJsonRpcMessage { Method = "test" };
        var act = async () => await transport.SendAsync(msg);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReceiveAsync_NotConnected_Throws()
    {
        using var transport = new StdioMcpTransport("echo");
        var act = async () => await transport.ReceiveAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Dispose_MultipleCallsIsSafe()
    {
        var transport = new StdioMcpTransport("echo");
        transport.Dispose();
        transport.Dispose();
    }

    [Fact]
    public async Task DisconnectAsync_NotConnected_DoesNotThrow()
    {
        using var transport = new StdioMcpTransport("echo");
        await transport.DisconnectAsync();
    }
}

#endregion

#region SkillOptions & SkillServiceCollectionExtensions

public class SkillOptionsTests
{
    [Fact]
    public void Defaults()
    {
        var opts = new SkillOptions();
        opts.AdditionalPaths.Should().BeEmpty();
        opts.ScanStandardPaths.Should().BeTrue();
        opts.AutoDiscover.Should().BeTrue();
    }

    [Fact]
    public void SetValues()
    {
        var opts = new SkillOptions
        {
            ScanStandardPaths = false,
            AutoDiscover = false,
            AdditionalPaths = new List<string> { "/custom/path" }
        };
        opts.ScanStandardPaths.Should().BeFalse();
        opts.AutoDiscover.Should().BeFalse();
        opts.AdditionalPaths.Should().Contain("/custom/path");
    }
}

public class SkillServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAgentSkills_DefaultOptions_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddAgentSkills();
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<SkillOptions>().Should().NotBeNull();
        sp.GetRequiredService<SkillDiscovery>().Should().NotBeNull();
    }

    [Fact]
    public void AddAgentSkills_CustomOptions()
    {
        var services = new ServiceCollection();
        services.AddAgentSkills(opts =>
        {
            opts.ScanStandardPaths = false;
            opts.AutoDiscover = false;
        });
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<SkillOptions>().ScanStandardPaths.Should().BeFalse();
    }

    [Fact]
    public void AddAgentSkills_AutoDiscover_RegistersProviders()
    {
        var services = new ServiceCollection();
        services.AddAgentSkills(opts => opts.ScanStandardPaths = false);
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<SkillToolProvider>().Should().NotBeNull();
        sp.GetRequiredService<SkillContextSource>().Should().NotBeNull();
        sp.GetServices<IToolProvider>().Should().NotBeEmpty();
        sp.GetServices<IContextSource>().Should().NotBeEmpty();
    }
}

#endregion

#region Agent ServiceCollectionExtensions

public class AgentServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAgentTooling_DefaultOptions_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddAgentTooling();
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<AgentToolingOptions>().Should().NotBeNull();
        sp.GetRequiredService<ToolRegistry>().Should().NotBeNull();
    }

    [Fact]
    public void AddAgentTooling_CustomOptions()
    {
        var services = new ServiceCollection();
        services.AddAgentTooling(opts => opts.MaxToolConcurrency = 16);
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<AgentToolingOptions>().MaxToolConcurrency.Should().Be(16);
    }

    [Fact]
    public void AddAgentTooling_WithToolProvider_RegistersInRegistry()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IToolProvider>(new TestToolProvider());
        services.AddAgentTooling();
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<ToolRegistry>();
        registry.Should().NotBeNull();
    }

    private class TestToolProvider : IToolProvider
    {
        public Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ToolDefinition>>(Array.Empty<ToolDefinition>());
        public Task<ToolResult> InvokeToolAsync(string toolName, string argumentsJson, CancellationToken ct = default)
            => Task.FromResult(new ToolResult { Content = "" });
    }
}

#endregion

#region Messaging ServiceCollectionExtensions

public class MessagingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInMemoryMessageConnector_DefaultName()
    {
        var services = new ServiceCollection();
        services.AddInMemoryMessageConnector();
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<InMemoryMessageConnector>().Should().NotBeNull();
    }

    [Fact]
    public void AddInMemoryMessageConnector_CustomName()
    {
        var services = new ServiceCollection();
        services.AddInMemoryMessageConnector("custom");
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<InMemoryMessageConnector>().Should().NotBeNull();
    }

    [Fact]
    public void AddMessageConnector_RegistersInstance()
    {
        var services = new ServiceCollection();
        var connector = new InMemoryMessageConnector("test");
        services.AddMessageConnector(connector);
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<InMemoryMessageConnector>().Should().BeSameAs(connector);
    }
}

#endregion

#region DI ServiceCollectionExtensions Extended

public class DIServiceCollectionExtendedTests
{
    [Fact]
    public void AddStep_TypedStepRegistered()
    {
        var services = new ServiceCollection();
        WorkflowFramework.Extensions.DependencyInjection.ServiceCollectionExtensions.AddStep<TestStep, TestData>(services);
        services.Should().Contain(sd => sd.ServiceType == typeof(TestStep));
    }

    [Fact]
    public void AddWorkflowMiddleware_Registered()
    {
        var services = new ServiceCollection();
        WorkflowFramework.Extensions.DependencyInjection.ServiceCollectionExtensions.AddWorkflowMiddleware<TestMiddleware>(services);
        services.Should().Contain(sd => sd.ServiceType == typeof(IWorkflowMiddleware));
    }

    [Fact]
    public void AddWorkflowEvents_Registered()
    {
        var services = new ServiceCollection();
        WorkflowFramework.Extensions.DependencyInjection.ServiceCollectionExtensions.AddWorkflowEvents<TestEvents>(services);
        services.Should().Contain(sd => sd.ServiceType == typeof(IWorkflowEvents));
    }

    private class TestData { }
    private class TestStep : IStep<TestData>
    {
        public string Name => "test";
        public Task ExecuteAsync(IWorkflowContext<TestData> context) => Task.CompletedTask;
    }

    private class TestMiddleware : IWorkflowMiddleware
    {
        public Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next) => next(context);
    }

    private class TestEvents : WorkflowEventsBase { }
}

#endregion

#region SkillDiscovery Extended

public class SkillDiscoveryExtendedTests
{
    [Fact]
    public void ScanDirectory_NonExistent_ReturnsEmpty()
    {
        var discovery = new SkillDiscovery(false);
        discovery.ScanDirectory("C:\\nonexistent_path_xyz").Should().BeEmpty();
    }

    [Fact]
    public void ScanDirectory_NullPath_Throws()
    {
        var discovery = new SkillDiscovery(false);
        var act = () => discovery.ScanDirectory(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DiscoverAll_NoStandardPaths_OnlyAdditional()
    {
        var discovery = new SkillDiscovery(false, new[] { "C:\\nonexistent" });
        var results = discovery.DiscoverAll();
        results.Should().BeEmpty(); // no skills found in nonexistent dir
    }

    [Fact]
    public void ScanStandardPaths_ReturnsListEvenIfEmpty()
    {
        var discovery = new SkillDiscovery(true);
        var results = discovery.ScanStandardPaths();
        results.Should().NotBeNull();
    }
}

#endregion

#region McpJsonRpcError

public class McpJsonRpcErrorTests
{
    [Fact]
    public void Properties_SetAndGet()
    {
        var error = new McpJsonRpcError
        {
            Code = -32600,
            Message = "Invalid Request"
        };
        error.Code.Should().Be(-32600);
        error.Message.Should().Be("Invalid Request");
    }

    [Fact]
    public void JsonRoundTrip()
    {
        var error = new McpJsonRpcError { Code = -32601, Message = "Method not found" };
        var json = JsonSerializer.Serialize(error);
        var deserialized = JsonSerializer.Deserialize<McpJsonRpcError>(json);
        deserialized.Should().NotBeNull();
        deserialized!.Code.Should().Be(-32601);
        deserialized.Message.Should().Be("Method not found");
    }
}

#endregion

#region InMemoryWorkflowEvents Additional

public class InMemoryWorkflowEventsExtendedTests
{
    [Fact]
    public async Task AllEventsCaptured()
    {
        var events = new InMemoryWorkflowEvents();
        var ctx = new WorkflowContext();
        var step = new FakeStep("s1");

        await events.OnWorkflowStartedAsync(ctx);
        await events.OnStepStartedAsync(ctx, step);
        await events.OnStepCompletedAsync(ctx, step);
        await events.OnWorkflowCompletedAsync(ctx);

        events.WorkflowStarted.Should().HaveCount(1);
        events.StepStarted.Should().HaveCount(1);
        events.StepCompleted.Should().HaveCount(1);
        events.WorkflowCompleted.Should().HaveCount(1);
    }

    [Fact]
    public async Task FailureEventsCaptured()
    {
        var events = new InMemoryWorkflowEvents();
        var ctx = new WorkflowContext();
        var step = new FakeStep("s1");
        var ex = new Exception("boom");

        await events.OnStepFailedAsync(ctx, step, ex);
        await events.OnWorkflowFailedAsync(ctx, ex);

        events.StepFailed.Should().HaveCount(1);
        events.StepFailed[0].Exception.Message.Should().Be("boom");
        events.WorkflowFailed.Should().HaveCount(1);
    }
}

#endregion

#region StepTestBuilder Extended

public class StepTestBuilderExtendedTests
{
    [Fact]
    public async Task ExecuteAsync_WithProperties()
    {
        var builder = new StepTestBuilder()
            .WithProperty("key", "value")
            .WithCancellation(CancellationToken.None);
        var step = new FakeStep("test");
        var ctx = await builder.ExecuteAsync(step);
        ctx.Properties["key"].Should().Be("value");
    }

    [Fact]
    public async Task ExecuteAsync_Typed()
    {
        var builder = new StepTestBuilder()
            .WithProperty("key", "value");
        var step = new FakeStep<TestData>("test");
        var ctx = await builder.ExecuteAsync(step, new TestData { Value = "hello" });
        ctx.Data.Value.Should().Be("hello");
        ctx.Properties["key"].Should().Be("value");
    }

    private class TestData
    {
        public string Value { get; set; } = "";
    }
}

#endregion

#region TypedTrackingStep

public class TypedTrackingStepTests
{
    [Fact]
    public async Task ExecuteAsync_TracksInLog()
    {
        var step = new TypedTrackingStep<TestData>("MyStep");
        step.Name.Should().Be("MyStep");
        var ctx = new WorkflowContext<TestData>(new TestData(), CancellationToken.None);
        await step.ExecuteAsync(ctx);
        TrackingStep.GetLog(ctx).Should().Contain("MyStep");
    }

    [Fact]
    public void DefaultName()
    {
        var step = new TypedTrackingStep<TestData>();
        step.Name.Should().Be("TypedTrackingStep");
    }

    private class TestData { }
}

#endregion

#region WorkflowBuilder Coverage Gaps

public class WorkflowBuilderCoverageTests
{
    [Fact]
    public void GenericBuilder_CreatesTypedWorkflow()
    {
        var wf = Workflow.Create<TestData>("typed")
            .Step(new TypedTrackingStep<TestData>("S1"))
            .Build();
        wf.Name.Should().Be("typed");
    }

    [Fact]
    public async Task GenericBuilder_ExecutesTypedWorkflow()
    {
        var wf = Workflow.Create<TestData>("typed")
            .Step(new TypedTrackingStep<TestData>("S1"))
            .Build();
        var ctx = new WorkflowContext<TestData>(new TestData(), CancellationToken.None);
        var result = await wf.ExecuteAsync(ctx);
        result.Status.Should().Be(WorkflowStatus.Completed);
    }

    private class TestData { }
}

#endregion

#region Hosting ServiceCollectionExtensions

public class HostingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWorkflowFramework_Hosting_RegistersServices()
    {
        var services = new ServiceCollection();
        WorkflowFramework.Extensions.Hosting.HostingServiceCollectionExtensions.AddWorkflowFramework(services, opts =>
        {
            opts.MaxParallelism = 4;
            opts.DefaultTimeout = TimeSpan.FromMinutes(1);
        });
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<WorkflowFramework.Extensions.Hosting.WorkflowHostingOptions>().MaxParallelism.Should().Be(4);
    }

    [Fact]
    public void AddWorkflowHostedServices_RegistersScheduler()
    {
        var services = new ServiceCollection();
        WorkflowFramework.Extensions.Hosting.HostingServiceCollectionExtensions.AddWorkflowHostedServices(services);
        services.Should().Contain(sd => sd.ServiceType == typeof(WorkflowFramework.Extensions.Scheduling.IWorkflowScheduler));
    }
}

#endregion

#region WorkflowHostingOptions

public class WorkflowHostingOptionsTests
{
    [Fact]
    public void Defaults()
    {
        var opts = new WorkflowFramework.Extensions.Hosting.WorkflowHostingOptions();
        opts.MaxParallelism.Should().BeGreaterThan(0);
        opts.DefaultTimeout.Should().BeNull();
    }
}

#endregion
