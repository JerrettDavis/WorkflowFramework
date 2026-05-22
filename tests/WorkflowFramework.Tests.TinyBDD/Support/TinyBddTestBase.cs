using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Support;

/// <summary>
/// Base class for all WorkflowFramework TinyBDD scenario classes.
/// Wraps <see cref="TinyBddXunitBase"/> and exposes the xUnit output helper
/// so test classes only need a one-line constructor.
/// </summary>
public abstract class TinyBddTestBase : TinyBddXunitBase
{
    protected ITestOutputHelper Output { get; }

    protected TinyBddTestBase(ITestOutputHelper output) : base(output)
    {
        Output = output;
    }
}
