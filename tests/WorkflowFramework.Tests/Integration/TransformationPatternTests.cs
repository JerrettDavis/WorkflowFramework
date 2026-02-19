using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Integration.Abstractions;
using WorkflowFramework.Extensions.Integration.Transformation;
using Xunit;

namespace WorkflowFramework.Tests.Integration;

public class TransformationPatternTests
{
    #region ContentEnricher

    [Fact]
    public void ContentEnricher_NullAction_Throws()
    {
        var act = () => new ContentEnricherStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ContentEnricher_EnrichesContext()
    {
        var step = new ContentEnricherStep(ctx => { ctx.Properties["extra"] = 42; return Task.CompletedTask; });
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        context.Properties["extra"].Should().Be(42);
    }

    [Fact]
    public async Task ContentEnricher_FailingAction_Throws()
    {
        var step = new ContentEnricherStep(ctx => throw new InvalidOperationException("fail"));
        var context = new WorkflowContext();
        var act = () => step.ExecuteAsync(context);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void ContentEnricher_DefaultName()
    {
        new ContentEnricherStep(ctx => Task.CompletedTask).Name.Should().Be("ContentEnricher");
    }

    [Fact]
    public void ContentEnricher_CustomName()
    {
        new ContentEnricherStep(ctx => Task.CompletedTask, "Custom").Name.Should().Be("Custom");
    }

    #endregion

    #region ContentFilter

    [Fact]
    public void ContentFilter_NullAction_Throws()
    {
        var act = () => new ContentFilterStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ContentFilter_RemovesFields()
    {
        var step = new ContentFilterStep(ctx =>
        {
            ctx.Properties.Remove("secret");
            return Task.CompletedTask;
        });
        var context = new WorkflowContext();
        context.Properties["secret"] = "password";
        context.Properties["public"] = "data";
        await step.ExecuteAsync(context);
        context.Properties.Should().NotContainKey("secret");
        context.Properties["public"].Should().Be("data");
    }

    [Fact]
    public void ContentFilter_DefaultName()
    {
        new ContentFilterStep(ctx => Task.CompletedTask).Name.Should().Be("ContentFilter");
    }

    [Fact]
    public void ContentFilter_CustomName()
    {
        new ContentFilterStep(ctx => Task.CompletedTask, "Strip").Name.Should().Be("Strip");
    }

    #endregion

    #region ClaimCheck + ClaimRetrieve

    [Fact]
    public void ClaimCheckStep_NullStore_Throws()
    {
        var act = () => new ClaimCheckStep(null!, ctx => "payload");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ClaimCheckStep_NullSelector_Throws()
    {
        var store = Substitute.For<IClaimCheckStore>();
        var act = () => new ClaimCheckStep(store, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ClaimRetrieveStep_NullStore_Throws()
    {
        var act = () => new ClaimRetrieveStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ClaimCheck_RoundTrip()
    {
        var store = new InMemoryClaimCheckStore();
        var payload = new { Data = "large" };
        var checkStep = new ClaimCheckStep(store, ctx => ctx.Properties["payload"]!);
        var retrieveStep = new ClaimRetrieveStep(store);

        var context = new WorkflowContext();
        context.Properties["payload"] = payload;

        await checkStep.ExecuteAsync(context);
        context.Properties.Should().ContainKey(ClaimCheckStep.ClaimTicketKey);

        await retrieveStep.ExecuteAsync(context);
        context.Properties["__ClaimPayload"].Should().BeSameAs(payload);
    }

    [Fact]
    public async Task ClaimRetrieve_MissingTicket_Throws()
    {
        var store = new InMemoryClaimCheckStore();
        var step = new ClaimRetrieveStep(store);
        var context = new WorkflowContext();
        var act = () => step.ExecuteAsync(context);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ClaimRetrieve_CustomResultKey()
    {
        var store = new InMemoryClaimCheckStore();
        var checkStep = new ClaimCheckStep(store, ctx => "data");
        var retrieveStep = new ClaimRetrieveStep(store, "myKey");
        var context = new WorkflowContext();
        await checkStep.ExecuteAsync(context);
        await retrieveStep.ExecuteAsync(context);
        context.Properties["myKey"].Should().Be("data");
    }

    [Fact]
    public void ClaimCheckStep_Name() => new ClaimCheckStep(Substitute.For<IClaimCheckStore>(), ctx => "x").Name.Should().Be("ClaimCheck");

    [Fact]
    public void ClaimRetrieveStep_Name() => new ClaimRetrieveStep(Substitute.For<IClaimCheckStore>()).Name.Should().Be("ClaimRetrieve");

    #endregion

    #region Normalizer

    [Fact]
    public void Normalizer_NullFormatDetector_Throws()
    {
        var act = () => new NormalizerStep(null!, new Dictionary<string, IStep>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Normalizer_NullTranslators_Throws()
    {
        var act = () => new NormalizerStep(ctx => "json", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Normalizer_RoutesToCorrectTranslator()
    {
        var executed = "";
        var step = new NormalizerStep(
            ctx => "xml",
            new Dictionary<string, IStep>
            {
                ["json"] = new TestStep("json", ctx => { executed = "json"; return Task.CompletedTask; }),
                ["xml"] = new TestStep("xml", ctx => { executed = "xml"; return Task.CompletedTask; }),
            });
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        executed.Should().Be("xml");
    }

    [Fact]
    public async Task Normalizer_UnknownFormat_WithDefault()
    {
        var executed = "";
        var step = new NormalizerStep(
            ctx => "yaml",
            new Dictionary<string, IStep>(),
            new TestStep("default", ctx => { executed = "default"; return Task.CompletedTask; }));
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        executed.Should().Be("default");
    }

    [Fact]
    public async Task Normalizer_UnknownFormat_NoDefault_Throws()
    {
        var step = new NormalizerStep(ctx => "unknown", new Dictionary<string, IStep>());
        var context = new WorkflowContext();
        var act = () => step.ExecuteAsync(context);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*unknown*");
    }

    [Fact]
    public void Normalizer_Name() => new NormalizerStep(ctx => "", new Dictionary<string, IStep>()).Name.Should().Be("Normalizer");

    #endregion

    #region MessageTranslator

    [Fact]
    public void MessageTranslator_NullTranslator_Throws()
    {
        var act = () => new MessageTranslatorStep<string, int>(null!, ctx => "", "key");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MessageTranslator_NullInputSelector_Throws()
    {
        var translator = Substitute.For<IMessageTranslator<string, int>>();
        var act = () => new MessageTranslatorStep<string, int>(translator, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task MessageTranslator_TransformsData()
    {
        var translator = Substitute.For<IMessageTranslator<string, int>>();
        translator.TranslateAsync("hello", Arg.Any<CancellationToken>()).Returns(5);
        var step = new MessageTranslatorStep<string, int>(translator, ctx => "hello");
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        context.Properties["__TranslatedOutput"].Should().Be(5);
    }

    [Fact]
    public async Task MessageTranslator_CustomOutputKey()
    {
        var translator = Substitute.For<IMessageTranslator<int, string>>();
        translator.TranslateAsync(42, Arg.Any<CancellationToken>()).Returns("forty-two");
        var step = new MessageTranslatorStep<int, string>(translator, ctx => 42, "myOutput");
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        context.Properties["myOutput"].Should().Be("forty-two");
    }

    [Fact]
    public void MessageTranslator_Name()
    {
        var translator = Substitute.For<IMessageTranslator<string, string>>();
        new MessageTranslatorStep<string, string>(translator, ctx => "").Name.Should().Be("MessageTranslator");
    }

    #endregion

    #region Helpers

    private sealed class TestStep(string name, Func<IWorkflowContext, Task>? action = null) : IStep
    {
        public string Name { get; } = name;
        public Task ExecuteAsync(IWorkflowContext context) => action?.Invoke(context) ?? Task.CompletedTask;
    }

    private sealed class InMemoryClaimCheckStore : IClaimCheckStore
    {
        private readonly Dictionary<string, object> _store = new();
        public Task<string> StoreAsync(object payload, CancellationToken cancellationToken = default)
        {
            var ticket = Guid.NewGuid().ToString("N");
            _store[ticket] = payload;
            return Task.FromResult(ticket);
        }
        public Task<object> RetrieveAsync(string claimTicket, CancellationToken cancellationToken = default)
            => Task.FromResult(_store[claimTicket]);
    }

    #endregion
}
