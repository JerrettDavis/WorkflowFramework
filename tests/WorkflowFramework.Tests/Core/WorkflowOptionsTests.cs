using FluentAssertions;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class WorkflowOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new WorkflowOptions();
        opts.MaxParallelism.Should().Be(Environment.ProcessorCount);
        opts.DefaultTimeout.Should().BeNull();
        opts.EnableCompensation.Should().BeFalse();
        opts.DefaultMaxRetryAttempts.Should().Be(3);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var opts = new WorkflowOptions
        {
            MaxParallelism = 4,
            DefaultTimeout = TimeSpan.FromSeconds(30),
            EnableCompensation = true,
            DefaultMaxRetryAttempts = 5
        };
        opts.MaxParallelism.Should().Be(4);
        opts.DefaultTimeout.Should().Be(TimeSpan.FromSeconds(30));
        opts.EnableCompensation.Should().BeTrue();
        opts.DefaultMaxRetryAttempts.Should().Be(5);
    }
}
