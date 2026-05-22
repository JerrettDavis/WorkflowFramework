using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WorkflowFramework.Extensions.Approvals;
using WorkflowFramework.Extensions.Approvals.Email;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Email.Tests;

public sealed class EmailApprovalChannelTests
{
    private static readonly string ValidKey = Convert.ToBase64String(new byte[32]
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
    });

    private static EmailApprovalOptions BuildOptions() => new()
    {
        SmtpHost = "smtp.test",
        From = "approvals@test.com",
        ApproveUrlTemplate = "https://example.com/approve?t={token}",
        RejectUrlTemplate = "https://example.com/reject?t={token}",
        TokenSigningKey = ValidKey
    };

    private static (EmailApprovalChannel channel, IEmailSender sender, InMemoryApprovalStore store, PersistentApprovalService persistent) Build(
        EmailApprovalOptions? opts = null)
    {
        var options = opts ?? BuildOptions();
        var sender = Substitute.For<IEmailSender>();
        var store = new InMemoryApprovalStore();

        // PersistentApprovalService needs a never-returning inner channel for test purposes.
        var fakeInner = Substitute.For<IApprovalChannel>();
        fakeInner.Name.Returns("fake-inner");
        fakeInner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(Timeout.Infinite, ci.Arg<CancellationToken>());
                return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
            });

        var persistent = new PersistentApprovalService(fakeInner, store);
        var tokenService = new ApprovalTokenService(Options.Create(options));

        var channel = new EmailApprovalChannel(
            Options.Create(options),
            sender,
            store,
            new Lazy<PersistentApprovalService>(() => persistent),
            tokenService);

        return (channel, sender, store, persistent);
    }

    private static ApprovalRequest MakeRequest(
        IReadOnlyDictionary<string, object?> context,
        int required = 1,
        TimeSpan? timeout = null) =>
        new(
            Title: "Deploy to Production",
            Description: "Please review and approve.",
            Context: context,
            RequiredApprovers: required,
            Timeout: timeout ?? TimeSpan.FromHours(1),
            AllowedRoles: null);

    // ------------------------------------------------------------------
    // Missing recipients context → throws
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_MissingRecipientsContext_Throws()
    {
        var (channel, _, _, _) = Build();
        var request = MakeRequest(new Dictionary<string, object?>());

        var act = async () => await channel.RequestApprovalAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*recipients*");
    }

    [Fact]
    public async Task RequestApprovalAsync_EmptyRecipientsList_Throws()
    {
        var (channel, _, _, _) = Build();
        var request = MakeRequest(new Dictionary<string, object?>
        {
            ["recipients"] = new List<string>()
        });

        var act = async () => await channel.RequestApprovalAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ------------------------------------------------------------------
    // Single recipient → one send
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_OneRecipient_SendsOneEmail()
    {
        var (channel, sender, store, persistent) = Build();
        var request = MakeRequest(new Dictionary<string, object?>
        {
            ["recipients"] = new[] { "alice@example.com" }
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Resolve externally after a short delay.
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            var vote = new ApprovalRecord("alice@example.com", null, true, null, DateTimeOffset.UtcNow, "email");
            await persistent.ResolveExternalAsync(request.CorrelationId, vote, cts.Token);
        });

        // The email channel saves the pending record, so we need to rehydrate first.
        // Actually, the channel saves to store but the TCS is set via persistent.
        // We must rehydrate BEFORE the channel calls WaitForCompletionAsync.
        // The flow: channel saves to store, then calls WaitForCompletionAsync.
        // Rehydrate sets up the TCS so WaitForCompletionAsync can find it.
        // We must call Rehydrate before the channel does, so we intercept via a special setup:
        // In this test, we rehydrate with the known correlationId.
        persistent.Rehydrate(new PendingApproval(
            CorrelationId: request.CorrelationId,
            Request: request,
            PrimaryChannel: "email",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow + request.Timeout,
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject));

        // Save to store first (channel does idempotent save).
        await store.SaveAsync(new PendingApproval(
            CorrelationId: request.CorrelationId,
            Request: request,
            PrimaryChannel: "email",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow + request.Timeout,
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject));

        var response = await channel.RequestApprovalAsync(request, cts.Token);

        await sender.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        response.Approved.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Multiple recipients → N sends
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_MultipleRecipients_SendsNEmails()
    {
        var (channel, sender, store, persistent) = Build();
        var recipients = new[] { "alice@example.com", "bob@example.com", "carol@example.com" };
        var request = MakeRequest(new Dictionary<string, object?>
        {
            ["recipients"] = recipients
        }, required: 1);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        persistent.Rehydrate(new PendingApproval(
            CorrelationId: request.CorrelationId,
            Request: request,
            PrimaryChannel: "email",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow + request.Timeout,
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject));

        await store.SaveAsync(new PendingApproval(
            CorrelationId: request.CorrelationId,
            Request: request,
            PrimaryChannel: "email",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow + request.Timeout,
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject));

        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            var vote = new ApprovalRecord("alice@example.com", null, true, null, DateTimeOffset.UtcNow, "email");
            await persistent.ResolveExternalAsync(request.CorrelationId, vote, cts.Token);
        });

        await channel.RequestApprovalAsync(request, cts.Token);

        await sender.Received(3).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // Email body contains approve and reject URLs with valid tokens
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_EmailBody_ContainsApproveAndRejectUrls()
    {
        var opts = BuildOptions();
        var (channel, sender, store, persistent) = Build(opts);
        var request = MakeRequest(new Dictionary<string, object?>
        {
            ["recipients"] = new[] { "alice@example.com" }
        });

        persistent.Rehydrate(new PendingApproval(
            CorrelationId: request.CorrelationId,
            Request: request,
            PrimaryChannel: "email",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow + request.Timeout,
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject));

        await store.SaveAsync(new PendingApproval(
            CorrelationId: request.CorrelationId,
            Request: request,
            PrimaryChannel: "email",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow + request.Timeout,
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            var vote = new ApprovalRecord("alice@example.com", null, true, null, DateTimeOffset.UtcNow, "email");
            await persistent.ResolveExternalAsync(request.CorrelationId, vote, cts.Token);
        });

        await channel.RequestApprovalAsync(request, cts.Token);

        var sentMessages = sender.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IEmailSender.SendAsync))
            .Select(c => (EmailMessage)c.GetArguments()[0]!)
            .ToList();

        sentMessages.Should().HaveCount(1);
        var msg = sentMessages[0];

        msg.Body.Should().Contain("https://example.com/approve?t=");
        msg.Body.Should().Contain("https://example.com/reject?t=");

        // Tokens in URLs should be parseable by ApprovalTokenService.
        var tokenSvc = new ApprovalTokenService(Options.Create(opts));

        var approveToken = ExtractTokenFromUrl(msg.Body, "approve");
        tokenSvc.TryParse(approveToken, out var approvePayload).Should().BeTrue();
        approvePayload.CorrelationId.Should().Be(request.CorrelationId);
        approvePayload.ApproverId.Should().Be("alice@example.com");
        approvePayload.Decision.Should().BeTrue();

        var rejectToken = ExtractTokenFromUrl(msg.Body, "reject");
        tokenSvc.TryParse(rejectToken, out var rejectPayload).Should().BeTrue();
        rejectPayload.Decision.Should().BeFalse();
    }

    private static string ExtractTokenFromUrl(string html, string urlType)
    {
        // Find "https://example.com/{urlType}?t=" and extract the token.
        var marker = $"https://example.com/{urlType}?t=";
        var start = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        start.Should().BeGreaterThan(-1, $"URL for {urlType} should be in body");
        start += marker.Length;
        var end = html.IndexOfAny(new[] { '"', '\'', ' ', '<', '>' }, start);
        if (end < 0) end = html.Length;
        return html[start..end];
    }

    // ------------------------------------------------------------------
    // Context value as string[] is accepted
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_ContextAsStringArray_IsAccepted()
    {
        var (channel, sender, store, persistent) = Build();
        var request = MakeRequest(new Dictionary<string, object?>
        {
            ["recipients"] = new string[] { "alice@example.com" }
        });

        persistent.Rehydrate(CreatePending(request));
        await store.SaveAsync(CreatePending(request));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = ResolveAfterDelayAsync(persistent, request.CorrelationId, "alice@example.com", cts.Token);

        await channel.RequestApprovalAsync(request, cts.Token);

        await sender.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // Context value as IEnumerable<string> is accepted
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_ContextAsIEnumerable_IsAccepted()
    {
        var (channel, sender, store, persistent) = Build();
        var request = MakeRequest(new Dictionary<string, object?>
        {
            ["recipients"] = (IEnumerable<string>)new List<string> { "alice@example.com" }
        });

        persistent.Rehydrate(CreatePending(request));
        await store.SaveAsync(CreatePending(request));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = ResolveAfterDelayAsync(persistent, request.CorrelationId, "alice@example.com", cts.Token);

        await channel.RequestApprovalAsync(request, cts.Token);

        await sender.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // Context value as comma-separated string is parsed correctly
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_ContextAsCommaSeparatedString_ParsesRecipients()
    {
        var (channel, sender, store, persistent) = Build();
        var request = MakeRequest(new Dictionary<string, object?>
        {
            ["recipients"] = "alice@example.com, bob@example.com"
        });

        persistent.Rehydrate(CreatePending(request));
        await store.SaveAsync(CreatePending(request));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = ResolveAfterDelayAsync(persistent, request.CorrelationId, "alice@example.com", cts.Token);

        await channel.RequestApprovalAsync(request, cts.Token);

        await sender.Received(2).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // Single send failure: continues with others; still awaits completion
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_OneSendFailure_ContinuesWithOthers()
    {
        var (channel, sender, store, persistent) = Build();
        var request = MakeRequest(new Dictionary<string, object?>
        {
            ["recipients"] = new[] { "fail@example.com", "success@example.com" }
        }, required: 1);

        sender.SendAsync(
                Arg.Is<EmailMessage>(m => m.To.Contains("fail@example.com")),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SMTP failure"));

        persistent.Rehydrate(CreatePending(request));
        await store.SaveAsync(CreatePending(request));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = ResolveAfterDelayAsync(persistent, request.CorrelationId, "success@example.com", cts.Token);

        var response = await channel.RequestApprovalAsync(request, cts.Token);

        response.Should().NotBeNull();
        await sender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To.Contains("success@example.com")),
            Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // All sends fail → throws InvalidOperationException
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_AllSendsFail_Throws()
    {
        var (channel, sender, store, persistent) = Build();
        var request = MakeRequest(new Dictionary<string, object?>
        {
            ["recipients"] = new[] { "alice@example.com" }
        });

        sender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        persistent.Rehydrate(CreatePending(request));
        await store.SaveAsync(CreatePending(request));

        var act = async () => await channel.RequestApprovalAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*All*failed*");
    }

    // ------------------------------------------------------------------
    // Name property
    // ------------------------------------------------------------------

    [Fact]
    public void Name_IsEmail()
    {
        var (channel, _, _, _) = Build();
        channel.Name.Should().Be("email");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static PendingApproval CreatePending(ApprovalRequest request) =>
        new(
            CorrelationId: request.CorrelationId,
            Request: request,
            PrimaryChannel: "email",
            CreatedAt: DateTimeOffset.UtcNow,
            DeadlineAt: DateTimeOffset.UtcNow + request.Timeout,
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject);

    private static async Task ResolveAfterDelayAsync(
        PersistentApprovalService persistent,
        string correlationId,
        string approverId,
        CancellationToken ct)
    {
        await Task.Delay(50, ct);
        var vote = new ApprovalRecord(approverId, null, true, null, DateTimeOffset.UtcNow, "email");
        await persistent.ResolveExternalAsync(correlationId, vote, ct);
    }
}
