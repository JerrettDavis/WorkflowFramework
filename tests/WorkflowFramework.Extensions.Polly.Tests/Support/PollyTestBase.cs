using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Polly.Tests.Support;

/// <summary>
/// Base class for all Extensions.Polly TinyBDD scenario classes.
/// </summary>
public abstract class PollyTestBase : TinyBddXunitBase
{
    protected PollyTestBase(ITestOutputHelper output) : base(output) { }
}
