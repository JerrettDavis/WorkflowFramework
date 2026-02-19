using FluentAssertions;
using WorkflowFramework.Extensions.Http;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Http;

public class HttpCallStepTests
{
    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        FluentActions.Invoking(() => new HttpStep(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_Default()
    {
        var step = new HttpStep(new HttpStepOptions { Method = HttpMethod.Get });
        step.Name.Should().Be("HttpGET");
    }

    [Fact]
    public void Name_Custom()
    {
        var step = new HttpStep(new HttpStepOptions { Name = "MyHttp" });
        step.Name.Should().Be("MyHttp");
    }

    [Fact]
    public void HttpStepOptions_Defaults()
    {
        var o = new HttpStepOptions();
        o.Name.Should().BeNull();
        o.Url.Should().BeEmpty();
        o.Method.Should().Be(HttpMethod.Get);
        o.Headers.Should().BeEmpty();
        o.Body.Should().BeNull();
        o.ContentType.Should().Be("application/json");
        o.EnsureSuccessStatusCode.Should().BeTrue();
    }
}
