using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Distributed.Redis.Tests.Support;

/// <summary>
/// Base class for all Extensions.Distributed.Redis TinyBDD scenario classes.
/// </summary>
public abstract class RedisTestBase : TinyBddXunitBase
{
    protected RedisTestBase(ITestOutputHelper output) : base(output) { }
}
