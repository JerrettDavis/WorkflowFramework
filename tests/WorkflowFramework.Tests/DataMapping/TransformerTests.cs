using Xunit;
using FluentAssertions;
using WorkflowFramework.Extensions.DataMapping.Transformers;

namespace WorkflowFramework.Tests.DataMapping;

public class TransformerTests
{
    [Fact]
    public void ToUpper_TransformsCorrectly()
    {
        var t = new ToUpperTransformer();
        t.Transform("hello").Should().Be("HELLO");
        t.Transform(null).Should().BeNull();
    }

    [Fact]
    public void ToLower_TransformsCorrectly()
    {
        var t = new ToLowerTransformer();
        t.Transform("HELLO").Should().Be("hello");
    }

    [Fact]
    public void Trim_TransformsCorrectly()
    {
        var t = new TrimTransformer();
        t.Transform("  hello  ").Should().Be("hello");
    }

    [Fact]
    public void DateFormat_ReformatsDate()
    {
        var t = new DateFormatTransformer();
        var args = new Dictionary<string, string?> { ["outputFormat"] = "MM/dd/yyyy" };
        t.Transform("2024-01-15", args).Should().Be("01/15/2024");
    }

    [Fact]
    public void DateFormat_WithInputFormat()
    {
        var t = new DateFormatTransformer();
        var args = new Dictionary<string, string?> { ["inputFormat"] = "dd-MM-yyyy", ["outputFormat"] = "yyyy/MM/dd" };
        t.Transform("15-01-2024", args).Should().Be("2024/01/15");
    }

    [Fact]
    public void NumberFormat_FormatsCurrency()
    {
        var t = new NumberFormatTransformer();
        var args = new Dictionary<string, string?> { ["format"] = "N2" };
        t.Transform("1234.5", args).Should().Be("1,234.50");
    }

    [Fact]
    public void Boolean_ConvertsVariousInputs()
    {
        var t = new BooleanTransformer();
        t.Transform("yes").Should().Be("True");
        t.Transform("Y").Should().Be("True");
        t.Transform("1").Should().Be("True");
        t.Transform("true").Should().Be("True");
        t.Transform("no").Should().Be("False");
        t.Transform("0").Should().Be("False");
        t.Transform(null).Should().Be("False");
    }

    [Fact]
    public void Boolean_CustomOutputValues()
    {
        var t = new BooleanTransformer();
        var args = new Dictionary<string, string?> { ["trueValue"] = "Y", ["falseValue"] = "N" };
        t.Transform("yes", args).Should().Be("Y");
        t.Transform("no", args).Should().Be("N");
    }

    [Fact]
    public void RegexReplace_ReplacesPattern()
    {
        var t = new RegexReplaceTransformer();
        var args = new Dictionary<string, string?> { ["pattern"] = @"\d+", ["replacement"] = "#" };
        t.Transform("abc123def456", args).Should().Be("abc#def#");
    }

    [Fact]
    public void DefaultValue_ProvidesDefault()
    {
        var t = new DefaultValueTransformer();
        var args = new Dictionary<string, string?> { ["default"] = "N/A" };
        t.Transform(null, args).Should().Be("N/A");
        t.Transform("", args).Should().Be("N/A");
        t.Transform("existing", args).Should().Be("existing");
    }

    [Fact]
    public void Conditional_ReturnsCorrectBranch()
    {
        var t = new ConditionalTransformer();
        var args = new Dictionary<string, string?> { ["equals"] = "active", ["then"] = "YES", ["else"] = "NO" };
        t.Transform("active", args).Should().Be("YES");
        t.Transform("inactive", args).Should().Be("NO");
    }

    [Fact]
    public void Composite_ChainsTransformers()
    {
        var t = new CompositeTransformer("trimUpper", [new TrimTransformer(), new ToUpperTransformer()]);
        t.Transform("  hello  ").Should().Be("HELLO");
    }
}
