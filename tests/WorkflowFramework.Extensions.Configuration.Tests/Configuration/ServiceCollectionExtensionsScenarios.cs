using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using WorkflowFramework.Extensions.Configuration;

namespace WorkflowFramework.Extensions.Configuration.Tests.Configuration;

[Feature("WorkflowDefinitionLoaderServiceCollectionExtensions — DI registration helpers")]
public class ServiceCollectionExtensionsScenarios : TinyBddXunitBase
{
    public ServiceCollectionExtensionsScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("AddYamlWorkflowLoader registers IWorkflowDefinitionLoader as singleton"), Fact]
    public async Task AddYamlWorkflowLoader_RegistersLoader()
    {
        var provider = new ServiceCollection()
            .AddYamlWorkflowLoader()
            .BuildServiceProvider();

        await Given("AddYamlWorkflowLoader registered", () => provider)
            .Then("IWorkflowDefinitionLoader is resolvable", p =>
            {
                var loader = p.GetService<IWorkflowDefinitionLoader>();
                loader.Should().NotBeNull().And.BeOfType<YamlWorkflowDefinitionLoader>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddJsonWorkflowLoader registers IWorkflowDefinitionLoader as singleton"), Fact]
    public async Task AddJsonWorkflowLoader_RegistersLoader()
    {
        var provider = new ServiceCollection()
            .AddJsonWorkflowLoader()
            .BuildServiceProvider();

        await Given("AddJsonWorkflowLoader registered", () => provider)
            .Then("IWorkflowDefinitionLoader resolves to JsonWorkflowDefinitionLoader", p =>
            {
                var loader = p.GetService<IWorkflowDefinitionLoader>();
                loader.Should().NotBeNull().And.BeOfType<JsonWorkflowDefinitionLoader>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddStepRegistry registers IStepRegistry as singleton"), Fact]
    public async Task AddStepRegistry_RegistersRegistry()
    {
        var provider = new ServiceCollection()
            .AddStepRegistry()
            .BuildServiceProvider();

        await Given("AddStepRegistry registered", () => provider)
            .Then("IStepRegistry is resolvable", p =>
            {
                var registry = p.GetService<IStepRegistry>();
                registry.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddWorkflowDefinitionBuilder registers WorkflowDefinitionBuilder as transient"), Fact]
    public async Task AddWorkflowDefinitionBuilder_RegistersBuilder()
    {
        var provider = new ServiceCollection()
            .AddStepRegistry()
            .AddWorkflowDefinitionBuilder()
            .BuildServiceProvider();

        await Given("AddWorkflowDefinitionBuilder registered", () => provider)
            .Then("WorkflowDefinitionBuilder is resolvable", p =>
            {
                var builder = p.GetService<WorkflowDefinitionBuilder>();
                builder.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }
}
