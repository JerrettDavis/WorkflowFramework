using System.Net;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Teams.Tests;

public sealed class TeamsApprovalChannelTests
{
    private const string WebhookUrl = "https://outlook.office.com/webhook/test";
    private const string Secret = "channel-test-secret-abcdef";

    private static TeamsApprovalOptions DefaultOptions() => new()
    {
        Mode = TeamsApprovalMode.IncomingWebhook,
        WebhookUrl = WebhookUrl,
        CallbackSharedSecret = Secret,
        HttpClientName = "approvals.teams",
    };

    private static ApprovalRequest MakeRequest(string correlationId = "corr-ch-001") =>
        new(
            Title: "Channel Test Approval",
            Description: "Testing Teams channel.",
            Context: new Dictionary<string, object?> { ["env"] = "test" },
            RequiredApprovers: 1,
            Timeout: TimeSpan.FromMinutes(5),
            AllowedRoles: null)
        {
            CorrelationId = correlationId
        };

    private static (
        TeamsApprovalChannel Channel,
        FakeHttpMessageHandler HttpHandler,
        IApprovalStore Store,
        PersistentApprovalService Persistent,
        TeamsCallbackTokenService Tokens)
    MakeChannel(TeamsApprovalOptions? options = null, HttpStatusCode responseCode = HttpStatusCode.OK)
    {
        var opts = options ?? DefaultOptions();
        var httpHandler = new FakeHttpMessageHandler(responseCode);
        var httpClient = new HttpClient(httpHandler);
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var store = Substitute.For<IApprovalStore>();
        store.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PendingApproval?>(null));

        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("teams");
        var persistent = new PersistentApprovalService(inner, store);

        var tokens = new TeamsCallbackTokenService(Options.Create(opts));
        var channel = new TeamsApprovalChannel(
            Options.Create(opts),
            httpFactory,
            store,
            new Lazy<PersistentApprovalService>(() => persistent),
            tokens);

        return (channel, httpHandler, store, persistent, tokens);
    }

    // ------------------------------------------------------------------
    // POSTs to WebhookUrl
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_PostsToWebhookUrl()
    {
        var (channel, httpHandler, store, persistent, _) = MakeChannel();
        var request = MakeRequest();

        // Register a TCS so WaitForCompletionAsync doesn't wait forever.
        var pending = MakePending(request.CorrelationId);
        persistent.Rehydrate(pending);
        // Complete immediately via DirectComplete after we fire the channel request.
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            persistent.DirectComplete(request.CorrelationId, ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>()));
        });

        // store.LoadAsync returns null so the channel will post.
        await channel.RequestApprovalAsync(request, CancellationToken.None);

        httpHandler.LastRequestUri.Should().NotBeNull();
        httpHandler.LastRequestUri!.ToString().Should().Be(WebhookUrl);
    }

    [Fact]
    public async Task RequestApprovalAsync_BodyContainsAdaptiveCardWithTitleAndDescription()
    {
        var (channel, httpHandler, _, persistent, _) = MakeChannel();
        var request = MakeRequest();

        var pending = MakePending(request.CorrelationId);
        persistent.Rehydrate(pending);
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            persistent.DirectComplete(request.CorrelationId, ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>()));
        });

        await channel.RequestApprovalAsync(request, CancellationToken.None);

        var body = httpHandler.LastRequestBody;
        body.Should().NotBeNullOrEmpty();
        body!.Should().Contain("Channel Test Approval");
        body.Should().Contain("Testing Teams channel.");
        body.Should().Contain("AdaptiveCard");
    }

    // ------------------------------------------------------------------
    // Non-2xx response throws
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_Non2xxResponse_ThrowsInvalidOperationException()
    {
        var (channel, _, _, _, _) = MakeChannel(responseCode: HttpStatusCode.InternalServerError);
        var request = MakeRequest("corr-fail");

        var act = async () => await channel.RequestApprovalAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ------------------------------------------------------------------
    // Cancellation propagates
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_CancellationBeforePost_ThrowsOperationCanceled()
    {
        // Use a slow HTTP handler to give cancellation time to kick in.
        var opts = DefaultOptions();
        var httpHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, delay: TimeSpan.FromSeconds(10));
        var httpClient = new HttpClient(httpHandler);
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var store = Substitute.For<IApprovalStore>();
        store.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PendingApproval?>(null));

        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("teams");
        var persistent = new PersistentApprovalService(inner, store);
        var tokens = new TeamsCallbackTokenService(Options.Create(opts));

        var channel = new TeamsApprovalChannel(Options.Create(opts), httpFactory, store, new Lazy<PersistentApprovalService>(() => persistent), tokens);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = async () => await channel.RequestApprovalAsync(MakeRequest("corr-cancel"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ------------------------------------------------------------------
    // 2xx response awaits persistent completion
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_2xxResponse_ReturnsApprovalResponse()
    {
        var (channel, _, _, persistent, _) = MakeChannel();
        var request = MakeRequest("corr-success");

        var pending = MakePending(request.CorrelationId);
        persistent.Rehydrate(pending);
        _ = Task.Run(async () =>
        {
            await Task.Delay(30);
            persistent.DirectComplete(request.CorrelationId, ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>()));
        });

        var response = await channel.RequestApprovalAsync(request, CancellationToken.None);

        response.Should().NotBeNull();
        response.Approved.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static PendingApproval MakePending(string correlationId) =>
        new(
            CorrelationId: correlationId,
            Request: new ApprovalRequest(
                Title: "Test",
                Description: null,
                Context: new Dictionary<string, object?>(),
                RequiredApprovers: 1,
                Timeout: TimeSpan.FromHours(1),
                AllowedRoles: null)
            {
                CorrelationId = correlationId
            },
            PrimaryChannel: "teams",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow.AddHours(1),
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject);

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly TimeSpan _delay;

        public Uri? LastRequestUri { get; private set; }
        public string? LastRequestBody { get; private set; }

        public FakeHttpMessageHandler(HttpStatusCode statusCode, TimeSpan delay = default)
        {
            _statusCode = statusCode;
            _delay = delay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            if (_delay > TimeSpan.Zero)
                await Task.Delay(_delay, cancellationToken);

            return new HttpResponseMessage(_statusCode);
        }
    }
}
