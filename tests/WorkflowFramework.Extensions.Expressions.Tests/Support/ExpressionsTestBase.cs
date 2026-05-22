using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Expressions.Tests.Support;

/// <summary>
/// Base class for all Extensions.Expressions TinyBDD scenario classes.
/// </summary>
public abstract class ExpressionsTestBase : TinyBddXunitBase
{
    protected ExpressionsTestBase(ITestOutputHelper output) : base(output) { }
}
