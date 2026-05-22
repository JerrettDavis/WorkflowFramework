using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals.Slack;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Slack.Tests;

public sealed class SlackApprovalChannelTests
{
    private static SlackApprovalOptions MakeOptions() =>
        new()
        {
            BotToken = "xoxb-test-token",
            ChannelId = "C12345",
            SigningSecret = "secret",
            ApiBaseUrl = "https://slack.com/api/",
            HttpClientName = "approvals.slack"
        };

    private static ApprovalRequest MakeRequest(string correlationId = "testcorr") =>
        new(
            Title: "Deploy Approval",
            Description: "Approve the deploy.",
            Context: new Dictionary<string, object?>(),
            RequiredApprovers: 1,
            Timeout: TimeSpan.FromMinutes(10),
            AllowedRoles: null)
        {
            CorrelationId = correlationId
        };

    /// <summary>
    /// Builds a channel with a persistent service that has the correlation pre-registered,
    /// so WaitForCompletionAsync won't throw. The inner channel never completes on its own —
    /// tests cancel after verifying the HTTP request.
    /// </summary>
    private static (SlackApprovalChannel channel, FakeHttpMessageHandler fakeHandler, IApprovalStore store, PersistentApprovalService persistent)
        MakeChannel(HttpResponseMessage? response = null, string correlationId = "testcorr")
    {
        var successResponse = response ?? new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true,"channel":"C12345","ts":"123.456"}""")
        };

        var fakeHandler = new FakeHttpMessageHandler(successResponse);
        var client = new HttpClient(fakeHandler);

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("approvals.slack").Returns(client);

        var store = Substitute.For<IApprovalStore>();
        store.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((PendingApproval?)null);
        store.SaveAsync(Arg.Any<PendingApproval>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Inner channel that blocks indefinitely — simulates a webhook-driven channel
        var innerChannel = Substitute.For<IApprovalChannel>();
        innerChannel.Name.Returns("test");
        innerChannel.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                return Task.Delay(Timeout.Infinite, ct)
                    .ContinueWith<ApprovalResponse>(_ => throw new OperationCanceledException(ct), ct);
            });

        var persistent = new PersistentApprovalService(
            innerChannel, store, NullLogger<PersistentApprovalService>.Instance);

        // Pre-register the correlation so WaitForCompletionAsync doesn't throw immediately
        var pendingRecord = new PendingApproval(
            CorrelationId: correlationId,
            Request: new ApprovalRequestBuilder()
                .WithTitle("Deploy Approval")
                .WithTimeout(TimeSpan.FromMinutes(10))
                .Build() with { CorrelationId = correlationId },
            PrimaryChannel: "slack",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow.AddMinutes(10),
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject);
        persistent.Rehydrate(pendingRecord);

        var channel = new SlackApprovalChannel(
            Options.Create(MakeOptions()),
            factory,
            store,
            new Lazy<PersistentApprovalService>(() => persistent),
            NullLogger<SlackApprovalChannel>.Instance);

        return (channel, fakeHandler, store, persistent);
    }

    [Fact]
    public async Task RequestApprovalAsync_PostsToCorrectUrl()
    {
        var (channel, fakeHandler, _, _) = MakeChannel();
        var request = MakeRequest();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        try { await channel.RequestApprovalAsync(request, cts.Token); }
        catch (OperationCanceledException) { }

        fakeHandler.LastRequestUri.Should().NotBeNull();
        fakeHandler.LastRequestUri!.ToString().Should().Be("https://slack.com/api/chat.postMessage");
    }

    [Fact]
    public async Task RequestApprovalAsync_SendsAuthorizationHeader()
    {
        var (channel, fakeHandler, _, _) = MakeChannel();
        var request = MakeRequest();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        try { await channel.RequestApprovalAsync(request, cts.Token); }
        catch (OperationCanceledException) { }

        fakeHandler.LastAuthorizationScheme.Should().Be("Bearer");
        fakeHandler.LastAuthorizationParameter.Should().Be("xoxb-test-token");
    }

    [Fact]
    public async Task RequestApprovalAsync_SetsContentTypeToApplicationJson()
    {
        var (channel, fakeHandler, _, _) = MakeChannel();
        var request = MakeRequest();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        try { await channel.RequestApprovalAsync(request, cts.Token); }
        catch (OperationCanceledException) { }

        fakeHandler.LastContentType.Should().Contain("application/json");
    }

    [Fact]
    public async Task RequestApprovalAsync_BodyContainsChannelAndBlocks()
    {
        var (channel, fakeHandler, _, _) = MakeChannel();
        var request = MakeRequest();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        try { await channel.RequestApprovalAsync(request, cts.Token); }
        catch (OperationCanceledException) { }

        fakeHandler.LastRequestBody.Should().Contain("C12345");
        fakeHandler.LastRequestBody.Should().Contain("blocks");
    }

    [Fact]
    public async Task RequestApprovalAsync_SlackReturnsOkFalse_ThrowsInvalidOperationException()
    {
        var errorResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":false,"error":"channel_not_found"}""")
        };
        var (channel, _, _, _) = MakeChannel(errorResponse);
        var request = MakeRequest();

        var act = async () => await channel.RequestApprovalAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*channel_not_found*");
    }

    [Fact]
    public async Task RequestApprovalAsync_UsesCorrectNamedHttpClient()
    {
        var correlationId = "checkname";
        var store = Substitute.For<IApprovalStore>();
        store.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((PendingApproval?)null);
        store.SaveAsync(Arg.Any<PendingApproval>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var fakeHandler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true,"channel":"C12345","ts":"1.2"}""")
        });

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("approvals.slack").Returns(new HttpClient(fakeHandler));

        var innerChannel = Substitute.For<IApprovalChannel>();
        innerChannel.Name.Returns("test");
        innerChannel.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                return Task.Delay(Timeout.Infinite, ct)
                    .ContinueWith<ApprovalResponse>(_ => throw new OperationCanceledException(ct), ct);
            });

        var persistent = new PersistentApprovalService(innerChannel, store);

        // Pre-register so WaitForCompletionAsync finds the TCS
        var pendingRecord = new PendingApproval(
            CorrelationId: correlationId,
            Request: new ApprovalRequestBuilder().WithTitle("T").WithTimeout(TimeSpan.FromMinutes(5)).Build() with { CorrelationId = correlationId },
            PrimaryChannel: "slack",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow.AddMinutes(5),
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject);
        persistent.Rehydrate(pendingRecord);

        var channel = new SlackApprovalChannel(
            Options.Create(MakeOptions()), factory, store, new Lazy<PersistentApprovalService>(() => persistent));

        var request = MakeRequest(correlationId);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        try { await channel.RequestApprovalAsync(request, cts.Token); }
        catch (OperationCanceledException) { }

        factory.Received(1).CreateClient("approvals.slack");
    }

    [Fact]
    public async Task RequestApprovalAsync_SlackOkTrue_AwaitsWaitForCompletion()
    {
        // Arrange: successful post, then wait for a vote that we inject via the store
        var correlationId = "waitcorr";
        var store = Substitute.For<IApprovalStore>();
        store.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((PendingApproval?)null);
        store.SaveAsync(Arg.Any<PendingApproval>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var fakeHandler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true,"channel":"C12345","ts":"1.2"}""")
        });
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("approvals.slack").Returns(new HttpClient(fakeHandler));

        var innerChannel = Substitute.For<IApprovalChannel>();
        innerChannel.Name.Returns("test");
        innerChannel.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                return Task.Delay(Timeout.Infinite, ct)
                    .ContinueWith<ApprovalResponse>(_ => throw new OperationCanceledException(ct), ct);
            });

        var persistent = new PersistentApprovalService(innerChannel, store);

        var pendingRecord = new PendingApproval(
            CorrelationId: correlationId,
            Request: new ApprovalRequestBuilder().WithTitle("T").WithTimeout(TimeSpan.FromMinutes(5)).Build() with { CorrelationId = correlationId },
            PrimaryChannel: "slack",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow.AddMinutes(5),
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject);
        persistent.Rehydrate(pendingRecord);

        var approvalVote = new ApprovalRecord("U1", "alice", true, null, DateTimeOffset.UtcNow, "slack");
        store.AppendVoteAsync(correlationId, Arg.Any<ApprovalRecord>(), Arg.Any<CancellationToken>())
            .Returns(pendingRecord with { Votes = new[] { approvalVote } });

        var channel = new SlackApprovalChannel(Options.Create(MakeOptions()), factory, store, new Lazy<PersistentApprovalService>(() => persistent));
        var request = MakeRequest(correlationId);

        // Start the channel request; in parallel, resolve the approval
        var channelTask = channel.RequestApprovalAsync(request, CancellationToken.None);

        // Give the HTTP call time to complete, then inject the vote
        await Task.Delay(50);
        await persistent.ResolveExternalAsync(correlationId, approvalVote, CancellationToken.None);

        var response = await channelTask;
        response.Approved.Should().BeTrue();
    }

    [Fact]
    public async Task RequestApprovalAsync_CancellationAfterHttpCall_PropagatesOperationCanceled()
    {
        // Use a rehydrated pending so WaitForCompletionAsync can start, then cancel
        var (channel, _, _, _) = MakeChannel(correlationId: "canceltest");
        var request = MakeRequest("canceltest");
        using var cts = new CancellationTokenSource();

        var channelTask = channel.RequestApprovalAsync(request, cts.Token);

        // Give the HTTP post time to go through, then cancel
        await Task.Delay(50);
        cts.Cancel();

        var act = async () => await channelTask;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// Fake HTTP message handler that captures request metadata before content is disposed.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public Uri? LastRequestUri { get; private set; }
        public string? LastAuthorizationScheme { get; private set; }
        public string? LastAuthorizationParameter { get; private set; }
        public string? LastContentType { get; private set; }
        public string LastRequestBody { get; private set; } = string.Empty;

        public FakeHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastAuthorizationScheme = request.Headers.Authorization?.Scheme;
            LastAuthorizationParameter = request.Headers.Authorization?.Parameter;
            LastContentType = request.Content?.Headers.ContentType?.ToString();

            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return _response;
        }
    }
}
