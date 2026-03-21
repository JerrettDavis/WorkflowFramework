using System.Reflection;
using FluentAssertions;
using WorkflowFramework;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Plugins;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Serialization;
using Xunit;

namespace WorkflowFramework.Dashboard.Api.Tests;

public sealed class WorkflowDefinitionCompilerTests
{
    [Theory]
    [InlineData("ollama", typeof(OllamaAgentProvider))]
    [InlineData("openai", typeof(OpenAiAgentProvider))]
    [InlineData("anthropic", typeof(AnthropicAgentProvider))]
    [InlineData("huggingface", typeof(HuggingFaceAgentProvider))]
    public void ResolveProvider_UsesConfiguredProvider(string provider, Type expectedType)
    {
        var settings = new DashboardSettingsService();
        settings.Update(new DashboardSettings
        {
            OllamaUrl = "http://localhost:11434",
            OpenAiApiKey = "test-openai-key",
            AnthropicApiKey = "test-anthropic-key",
            HuggingFaceApiKey = "test-huggingface-key"
        });

        var compiler = new WorkflowDefinitionCompiler(settings, new PluginRegistry());
        var resolveProvider = typeof(WorkflowDefinitionCompiler).GetMethod("ResolveProvider", BindingFlags.Instance | BindingFlags.NonPublic);

        resolveProvider.Should().NotBeNull();

        var providerInstance = resolveProvider!.Invoke(compiler, [new Dictionary<string, string> { ["provider"] = provider }]);
        providerInstance.Should().BeOfType(expectedType);
    }

    [Fact]
    public void ResolveProvider_ThrowsForUnsupportedProvider()
    {
        var compiler = new WorkflowDefinitionCompiler(new DashboardSettingsService(), new PluginRegistry());
        var resolveProvider = typeof(WorkflowDefinitionCompiler).GetMethod("ResolveProvider", BindingFlags.Instance | BindingFlags.NonPublic);

        resolveProvider.Should().NotBeNull();

        var act = () => resolveProvider!.Invoke(compiler, [new Dictionary<string, string> { ["provider"] = "unsupported" }]);
        var exception = act.Should().Throw<TargetInvocationException>().Which;
        exception.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("Unsupported AI provider");
    }

    [Fact]
    public void Compile_UsesAdvertisedAiStepTypes_AndPreservesLlmOptions()
    {
        var settings = new DashboardSettingsService();
        settings.Update(new DashboardSettings
        {
            OllamaUrl = "http://localhost:11434",
            OpenAiApiKey = "test-openai-key",
            AnthropicApiKey = "test-anthropic-key",
            HuggingFaceApiKey = "test-huggingface-key"
        });

        var compiler = new WorkflowDefinitionCompiler(settings, new PluginRegistry());
        var definition = new WorkflowDefinitionDto
        {
            Name = "Ai Config",
            Steps =
            [
                new StepDefinitionDto
                {
                    Name = "Call LLM",
                    Type = "LlmCallStep",
                    Config = new Dictionary<string, string>
                    {
                        ["provider"] = "openai",
                        ["model"] = "gpt-4o-mini",
                        ["prompt"] = "Summarize",
                        ["temperature"] = "0.4",
                        ["maxTokens"] = "256"
                    }
                },
                new StepDefinitionDto
                {
                    Name = "Loop",
                    Type = "AgentLoopStep",
                    Config = new Dictionary<string, string>
                    {
                        ["provider"] = "ollama",
                        ["model"] = "llama3.2",
                        ["systemPrompt"] = "Keep going"
                    }
                },
                new StepDefinitionDto
                {
                    Name = "Choose Route",
                    Type = "AgentDecisionStep",
                    Config = new Dictionary<string, string>
                    {
                        ["provider"] = "anthropic",
                        ["model"] = "claude-sonnet-4-20250514",
                        ["prompt"] = "Choose",
                        ["options"] = "[\"A\",\"B\"]"
                    }
                },
                new StepDefinitionDto
                {
                    Name = "Plan Work",
                    Type = "AgentPlanStep",
                    Config = new Dictionary<string, string>
                    {
                        ["provider"] = "huggingface",
                        ["model"] = "mistralai/Mistral-7B-Instruct-v0.3",
                        ["objective"] = "Break the task down"
                    }
                }
            ]
        };

        var workflow = compiler.Compile(definition).Should().BeOfType<WorkflowEngine>().Subject;

        workflow.Steps.Should().HaveCount(4);
        workflow.Steps[0].Should().BeOfType<LlmCallStep>();
        workflow.Steps[1].Should().BeOfType<AgentLoopStep>();
        workflow.Steps[2].Should().BeOfType<AgentDecisionStep>();
        workflow.Steps[3].Should().BeOfType<AgentPlanStep>();

        var llmOptions = GetPrivateField<LlmCallOptions>(workflow.Steps[0], "_options");
        llmOptions.Model.Should().Be("gpt-4o-mini");
        llmOptions.Temperature.Should().Be(0.4d);
        llmOptions.MaxTokens.Should().Be(256);

        var loopOptions = GetPrivateField<AgentLoopOptions>(workflow.Steps[1], "_options");
        loopOptions.MaxIterations.Should().Be(10);
    }

    [Fact]
    public async Task Compile_SupportsCaseInsensitiveBuiltIns_Timeout_AndStoredSubWorkflows()
    {
        var store = new InMemoryWorkflowDefinitionStore();
        await store.SeedAsync(new SavedWorkflowDefinition
        {
            Id = "child-flow",
            Definition = new WorkflowDefinitionDto
            {
                Name = "Child Flow",
                Steps =
                [
                    new StepDefinitionDto
                    {
                        Name = "ChildAction",
                        Type = "Action",
                        Config = new Dictionary<string, string> { ["expression"] = "child" }
                    }
                ]
            }
        });

        var compiler = new WorkflowDefinitionCompiler(new DashboardSettingsService(), new PluginRegistry(), store);
        var definition = new WorkflowDefinitionDto
        {
            Name = "Parent",
            Steps =
            [
                new StepDefinitionDto
                {
                    Name = "LowercaseAction",
                    Type = "action",
                    Config = new Dictionary<string, string> { ["expression"] = "parent" }
                },
                new StepDefinitionDto
                {
                    Name = "CallChild",
                    Type = "SubWorkflow",
                    SubWorkflowName = "Child Flow"
                },
                new StepDefinitionDto
                {
                    Name = "Guard",
                    Type = "Timeout",
                    TimeoutSeconds = 5,
                    Inner = new StepDefinitionDto
                    {
                        Name = "InnerAction",
                        Type = "Action",
                        Config = new Dictionary<string, string> { ["expression"] = "inner" }
                    }
                }
            ]
        };

        var workflow = compiler.Compile(definition).Should().BeOfType<WorkflowEngine>().Subject;

        workflow.Steps.Should().HaveCount(3);
        workflow.Steps[0].GetType().Name.Should().Be("DelegateStep");
        workflow.Steps[1].GetType().Name.Should().Be("SubWorkflowStep");
        workflow.Steps[2].GetType().Name.Should().Be("TimeoutStep");
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("true", true)]
    [InlineData("0", false)]
    [InlineData("1", true)]
    public async Task Compile_Conditional_HandlesLiteralExpressions(string expression, bool expectedThenBranch)
    {
        var compiler = new WorkflowDefinitionCompiler(new DashboardSettingsService(), new PluginRegistry());
        var definition = new WorkflowDefinitionDto
        {
            Name = "Conditional",
            Steps =
            [
                new StepDefinitionDto
                {
                    Name = "Route",
                    Type = "Conditional",
                    Config = new Dictionary<string, string> { ["expression"] = expression },
                    Then = new StepDefinitionDto
                    {
                        Name = "ThenStep",
                        Type = "Action",
                        Config = new Dictionary<string, string> { ["expression"] = "then" }
                    },
                    Else = new StepDefinitionDto
                    {
                        Name = "ElseStep",
                        Type = "Action",
                        Config = new Dictionary<string, string> { ["expression"] = "else" }
                    }
                }
            ]
        };

        var workflow = compiler.Compile(definition);
        var context = new WorkflowContext();

        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        context.Properties.ContainsKey("ThenStep.Output").Should().Be(expectedThenBranch);
        context.Properties.ContainsKey("ElseStep.Output").Should().Be(!expectedThenBranch);
    }

    [Fact]
    public void Compile_TryCatch_HonorsConfiguredCatchTypes()
    {
        var compiler = new WorkflowDefinitionCompiler(new DashboardSettingsService(), new PluginRegistry());
        var definition = new WorkflowDefinitionDto
        {
            Name = "TryCatch",
            Steps =
            [
                new StepDefinitionDto
                {
                    Name = "Guard",
                    Type = "TryCatch",
                    CatchTypes = [typeof(InvalidOperationException).FullName!, typeof(ArgumentException).FullName!],
                    TryBody =
                    [
                        new StepDefinitionDto
                        {
                            Name = "Inner",
                            Type = "Action",
                            Config = new Dictionary<string, string> { ["expression"] = "work" }
                        }
                    ]
                }
            ]
        };

        var workflow = compiler.Compile(definition).Should().BeOfType<WorkflowEngine>().Subject;
        workflow.Steps.Should().HaveCount(1);
        workflow.Steps[0].GetType().Name.Should().Be("TryCatchStep");

        var field = GetFieldInfo(workflow.Steps[0].GetType(), "catchHandlers");
        field.Should().NotBeNull();

        var handlers = field!.GetValue(workflow.Steps[0]).Should().BeAssignableTo<System.Collections.IDictionary>().Subject;
        handlers.Keys.Cast<Type>().Should().BeEquivalentTo([typeof(InvalidOperationException), typeof(ArgumentException)]);
    }

    private static T GetPrivateField<T>(object instance, string fieldName) where T : class
    {
        var field = GetFieldInfo(instance.GetType(), fieldName);
        field.Should().NotBeNull();
        return field!.GetValue(instance).Should().BeOfType<T>().Subject;
    }

    private static FieldInfo? GetFieldInfo(Type type, string fieldName)
    {
        for (var current = type; current is not null; current = current.BaseType!)
        {
            var field = current.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?? current.GetField($"<{fieldName}>P", BindingFlags.Instance | BindingFlags.NonPublic);

            if (field is not null)
                return field;
        }

        return null;
    }
}
