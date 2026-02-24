using FluentAssertions;
using WorkflowFramework.Triggers;
using WorkflowFramework.Triggers.Sources;
using Xunit;

namespace WorkflowFramework.Tests.Triggers;

public class TriggerSourceFactoryTests
{
    [Fact]
    public void Create_Schedule_ReturnsScheduleSource()
    {
        var factory = new TriggerSourceFactory();
        var source = factory.Create(new TriggerDefinition { Type = "schedule" });
        source.Should().BeOfType<ScheduleTriggerSource>();
        source.Type.Should().Be("schedule");
    }

    [Fact]
    public void Create_FileWatch_ReturnsFileWatchSource()
    {
        var factory = new TriggerSourceFactory();
        var source = factory.Create(new TriggerDefinition { Type = "filewatch" });
        source.Should().BeOfType<FileWatchTriggerSource>();
    }

    [Fact]
    public void Create_Manual_ReturnsManualSource()
    {
        var factory = new TriggerSourceFactory();
        var source = factory.Create(new TriggerDefinition { Type = "manual" });
        source.Should().BeOfType<ManualTriggerSource>();
    }

    [Fact]
    public void Create_Audio_ReturnsAudioSource()
    {
        var factory = new TriggerSourceFactory();
        var source = factory.Create(new TriggerDefinition { Type = "audio" });
        source.Should().BeOfType<AudioInputTriggerSource>();
    }

    [Fact]
    public void Create_UnknownType_Throws()
    {
        var factory = new TriggerSourceFactory();
        var act = () => factory.Create(new TriggerDefinition { Type = "unknown" });
        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("unknown");
    }

    [Fact]
    public void Create_NullDefinition_Throws()
    {
        var factory = new TriggerSourceFactory();
        var act = () => factory.Create(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetAvailableTypes_ReturnsBuiltInTypes()
    {
        var factory = new TriggerSourceFactory();
        var types = factory.GetAvailableTypes();
        types.Should().HaveCountGreaterThanOrEqualTo(4);
        types.Select(t => t.Type).Should().Contain("schedule");
        types.Select(t => t.Type).Should().Contain("filewatch");
        types.Select(t => t.Type).Should().Contain("manual");
        types.Select(t => t.Type).Should().Contain("audio");
    }

    [Fact]
    public void Register_CustomType_Works()
    {
        var factory = new TriggerSourceFactory();
        factory.Register("custom", def => new ManualTriggerSource(def), new TriggerTypeInfo
        {
            Type = "custom",
            DisplayName = "Custom",
            Description = "A custom trigger"
        });
        var source = factory.Create(new TriggerDefinition { Type = "custom" });
        source.Should().BeOfType<ManualTriggerSource>();
        factory.GetAvailableTypes().Select(t => t.Type).Should().Contain("custom");
    }

    [Fact]
    public void Register_OverridesExisting()
    {
        var factory = new TriggerSourceFactory();
        factory.Register("manual", def => new ManualTriggerSource(def));
        // Should not throw — override is allowed
        factory.Create(new TriggerDefinition { Type = "manual" }).Should().NotBeNull();
    }

    [Fact]
    public void GetAvailableTypes_HasConfigSchemas()
    {
        var factory = new TriggerSourceFactory();
        var types = factory.GetAvailableTypes();
        foreach (var t in types)
        {
            t.DisplayName.Should().NotBeNullOrEmpty();
            t.Category.Should().NotBeNullOrEmpty();
        }
    }
}
