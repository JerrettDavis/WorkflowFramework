using Xunit;
using FluentAssertions;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Engine;
using WorkflowFramework.Extensions.DataMapping.Transformers;

namespace WorkflowFramework.Tests.DataMapping;

public class FieldTransformerRegistryTests
{
    [Fact]
    public void Get_RegisteredTransformer_Returns()
    {
        var registry = new FieldTransformerRegistry([new ToUpperTransformer()]);
        registry.Get("toUpper").Should().NotBeNull();
    }

    [Fact]
    public void Get_Unknown_ReturnsNull()
    {
        var registry = new FieldTransformerRegistry();
        registry.Get("missing").Should().BeNull();
    }

    [Fact]
    public void ApplyAll_NullPipeline_ReturnsOriginal()
    {
        var registry = new FieldTransformerRegistry();
        registry.ApplyAll("hello", null).Should().Be("hello");
    }

    [Fact]
    public void ApplyAll_ChainedTransformers_AppliesInOrder()
    {
        var registry = new FieldTransformerRegistry([new ToUpperTransformer(), new TrimTransformer()]);
        var chain = new[]
        {
            new TransformerRef("trim"),
            new TransformerRef("toUpper")
        };
        registry.ApplyAll("  hello  ", chain).Should().Be("HELLO");
    }

    [Fact]
    public void Register_Duplicate_Throws()
    {
        var registry = new FieldTransformerRegistry([new ToUpperTransformer()]);
        var act = () => registry.Register(new ToUpperTransformer());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegisteredNames_ReturnsAllNames()
    {
        var registry = new FieldTransformerRegistry([new ToUpperTransformer(), new ToLowerTransformer()]);
        registry.RegisteredNames.Should().Contain("toUpper").And.Contain("toLower");
    }
}
