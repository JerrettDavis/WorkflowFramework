using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Reactive.Tests.Support;

/// <summary>
/// Base class for all Extensions.Reactive TinyBDD scenario classes.
/// </summary>
public abstract class ReactiveTestBase : TinyBddXunitBase
{
    protected ReactiveTestBase(ITestOutputHelper output) : base(output) { }
}
