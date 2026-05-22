using FluentAssertions;
using Reqnroll;
using WorkflowFramework.Extensions.Approvals;
using WorkflowFramework.Extensions.Approvals.Acceptance.Support;

namespace WorkflowFramework.Extensions.Approvals.Acceptance.StepDefinitions;

[Binding]
public sealed class ApprovalSteps
{
    // ScenarioContext keys
    private const string KeyService = "Service";
    private const string KeyStore = "Store";
    private const string KeyFakeChannel = "FakeChannel";
    private const string KeyRequest = "Request";
    private const string KeyApprovalTask = "ApprovalTask";
    private const string KeyResponse = "Response";
    private const string KeyLastVoteException = "LastVoteException";
    private const string KeyCorrelationId = "CorrelationId";
    private const string KeyPrimaryChannel = "PrimaryChannel";
    private const string KeySecondaryChannel = "SecondaryChannel";
    private const string KeyCompositeChannel = "CompositeChannel";
    private const string KeyRehydratedService = "RehydratedService";
    private const string KeyRehydratedTask = "RehydratedTask";

    private readonly ScenarioContext _context;

    public ApprovalSteps(ScenarioContext context)
    {
        _context = context;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private PersistentApprovalService Service =>
        _context.Get<PersistentApprovalService>(KeyService);

    private InMemoryApprovalStore Store =>
        _context.Get<InMemoryApprovalStore>(KeyStore);

    private FakeApprovalChannel FakeChannel =>
        _context.Get<FakeApprovalChannel>(KeyFakeChannel);

    private ApprovalRequest CurrentRequest =>
        _context.Get<ApprovalRequest>(KeyRequest);

    private Task<ApprovalResponse> ApprovalTask =>
        _context.Get<Task<ApprovalResponse>>(KeyApprovalTask);

    private static ApprovalRecord MakeVote(
        string approverId,
        bool approved,
        string? displayName = null,
        string? comment = null,
        string channel = "fake") =>
        new(approverId, displayName ?? approverId, approved, comment,
            DateTimeOffset.UtcNow, channel);

    private void SetupPersistentService(ApprovalRequest request, FakeApprovalChannel? channel = null)
    {
        var store = new InMemoryApprovalStore();
        var fake = channel ?? new FakeApprovalChannel();
        var svc = new PersistentApprovalService(fake, store);

        _context.Set(store, KeyStore);
        _context.Set(fake, KeyFakeChannel);
        _context.Set(svc, KeyService);
        _context.Set(request, KeyRequest);
        _context.Set(request.CorrelationId, KeyCorrelationId);

        var task = svc.RequestApprovalAsync(request);
        _context.Set(task, KeyApprovalTask);
    }

    private async Task SubmitVoteAsync(
        string approverId,
        bool approved,
        string? displayName = null,
        string? comment = null)
    {
        var vote = MakeVote(approverId, approved, displayName, comment);
        await Service.ResolveExternalAsync(CurrentRequest.CorrelationId, vote);
    }

    private async Task<ApprovalResponse> GetOrAwaitResponseAsync()
    {
        if (_context.TryGetValue(KeyResponse, out ApprovalResponse cached))
            return cached;

        var response = await ApprovalTask.WaitAsync(TimeSpan.FromSeconds(5));
        _context.Set(response, KeyResponse);
        return response;
    }

    private async Task AssertOutcomeAsync(ApprovalOutcome expected)
    {
        var response = await GetOrAwaitResponseAsync();
        response.Outcome.Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // Background step
    // -------------------------------------------------------------------------

    [Given("an approvals service configured with quorum support")]
    public void GivenAnApprovalsServiceConfiguredWithQuorumSupport()
    {
        // Intentionally empty — the service is built per-scenario.
    }

    // -------------------------------------------------------------------------
    // Quorum / general vote steps
    // -------------------------------------------------------------------------

    [Given(@"a pending approval requiring (\d+) approvers? out of (\d+) candidates")]
    public void GivenPendingApprovalRequiringApproversOutOfCandidates(int required, int total)
    {
        // Use the well-known test approver names as AllowedRoles so that
        // totalAddressableApprovers == total for accurate quorum math.
        var knownApprovers = new[] { "alice", "bob", "charlie", "dave", "eve" };
        var roles = knownApprovers.Take(total).ToArray();

        var request = new ApprovalRequestBuilder()
            .WithTitle("Quorum Test")
            .WithTimeout(TimeSpan.FromSeconds(30))
            .RequiringApprovers(required)
            .AllowedFor(roles)
            .Build();

        SetupPersistentService(request);
    }

    [Given(@"a pending approval requiring (\d+) approvers")]
    public void GivenPendingApprovalRequiringApprovers(int required)
    {
        var request = new ApprovalRequestBuilder()
            .WithTitle("Approval Test")
            .WithTimeout(TimeSpan.FromSeconds(30))
            .RequiringApprovers(required)
            .Build();

        SetupPersistentService(request);
    }

    [When(@"approver ""([^""]+)"" approves")]
    public async Task WhenApproverApproves(string approverId)
    {
        await SubmitVoteAsync(approverId, approved: true);
    }

    [When(@"approver ""([^""]+)"" rejects")]
    public async Task WhenApproverRejects(string approverId)
    {
        await SubmitVoteAsync(approverId, approved: false);
    }

    // -------------------------------------------------------------------------
    // Then — outcome assertions (individual steps to avoid regex alternation issues)
    // -------------------------------------------------------------------------

    [Then("the approval response is Approved")]
    public async Task ThenTheApprovalResponseIsApproved()
    {
        await AssertOutcomeAsync(ApprovalOutcome.Approved);
    }

    [Then("the approval response is Rejected")]
    public async Task ThenTheApprovalResponseIsRejected()
    {
        await AssertOutcomeAsync(ApprovalOutcome.Rejected);
    }

    [Then("the approval response is TimedOut")]
    public async Task ThenTheApprovalResponseIsTimedOut()
    {
        await AssertOutcomeAsync(ApprovalOutcome.TimedOut);
    }

    [Then("the approval response is Escalated")]
    public async Task ThenTheApprovalResponseIsEscalated()
    {
        await AssertOutcomeAsync(ApprovalOutcome.Escalated);
    }

    [Then("the approval outcome is Approved")]
    public async Task ThenTheApprovalOutcomeIsApproved()
    {
        await AssertOutcomeAsync(ApprovalOutcome.Approved);
    }

    [Then("the approval outcome is Rejected")]
    public async Task ThenTheApprovalOutcomeIsRejected()
    {
        await AssertOutcomeAsync(ApprovalOutcome.Rejected);
    }

    [Then("the approval outcome is TimedOut")]
    public async Task ThenTheApprovalOutcomeIsTimedOut()
    {
        await AssertOutcomeAsync(ApprovalOutcome.TimedOut);
    }

    [Then("the approval outcome is Escalated")]
    public async Task ThenTheApprovalOutcomeIsEscalated()
    {
        await AssertOutcomeAsync(ApprovalOutcome.Escalated);
    }

    [Then(@"the response includes (\d+) approval records?")]
    public async Task ThenTheResponseIncludesApprovalRecords(int count)
    {
        var response = await GetOrAwaitResponseAsync();
        response.Approvals.Should().HaveCount(count);
    }

    [Then(@"exactly (\d+) approval records? is present")]
    public async Task ThenExactlyApprovalRecordIsPresent(int count)
    {
        var response = await GetOrAwaitResponseAsync();
        response.Approvals.Should().HaveCount(count);
    }

    // -------------------------------------------------------------------------
    // Timeout scenario steps
    // -------------------------------------------------------------------------

    [Given(@"a pending approval with timeout (\d+) ms and on-timeout action AutoReject")]
    public void GivenPendingApprovalWithTimeoutAndAutoReject(int timeoutMs)
    {
        var slow = new SlowFakeApprovalChannel("inner");
        var timeoutChannel = new EscalatingTimeoutChannel(
            slow,
            TimeSpan.FromMilliseconds(timeoutMs),
            OnTimeoutAction.AutoReject,
            escalationTarget: null);

        var request = new ApprovalRequestBuilder()
            .WithTitle("Timeout Test")
            .WithTimeout(TimeSpan.FromSeconds(30))
            .Build();

        _context.Set(request, KeyRequest);
        _context.Set(request.CorrelationId, KeyCorrelationId);

        var task = timeoutChannel.RequestApprovalAsync(request);
        _context.Set(task, KeyApprovalTask);
    }

    [Given(@"a pending approval with timeout (\d+) ms and on-timeout action AutoApprove")]
    public void GivenPendingApprovalWithTimeoutAndAutoApprove(int timeoutMs)
    {
        var slow = new SlowFakeApprovalChannel("inner");
        var timeoutChannel = new EscalatingTimeoutChannel(
            slow,
            TimeSpan.FromMilliseconds(timeoutMs),
            OnTimeoutAction.AutoApprove,
            escalationTarget: null);

        var request = new ApprovalRequestBuilder()
            .WithTitle("Timeout Test")
            .WithTimeout(TimeSpan.FromSeconds(30))
            .Build();

        _context.Set(request, KeyRequest);
        _context.Set(request.CorrelationId, KeyCorrelationId);

        var task = timeoutChannel.RequestApprovalAsync(request);
        _context.Set(task, KeyApprovalTask);
    }

    [Given(@"a pending approval requiring (\d+) approvers with timeout (\d+) ms and on-timeout action AutoReject")]
    public void GivenPendingApprovalRequiringApproversWithTimeoutAutoReject(int required, int timeoutMs)
    {
        // PersistentApprovalService with short timeout so votes can be submitted
        // before the deadline fires. The service reads votes from the store on timeout.
        var request = new ApprovalRequestBuilder()
            .WithTitle("Timeout Partial Votes Test")
            .WithTimeout(TimeSpan.FromMilliseconds(timeoutMs))
            .RequiringApprovers(required)
            .Build();

        SetupPersistentService(request);
    }

    [When("the timeout elapses with no votes")]
    public async Task WhenTheTimeoutElapsesWithNoVotes()
    {
        var response = await ApprovalTask.WaitAsync(TimeSpan.FromSeconds(10));
        _context.Set(response, KeyResponse);
    }

    [When("the timeout elapses")]
    public async Task WhenTheTimeoutElapses()
    {
        var response = await ApprovalTask.WaitAsync(TimeSpan.FromSeconds(10));
        _context.Set(response, KeyResponse);
    }

    // -------------------------------------------------------------------------
    // Escalation scenario steps
    // -------------------------------------------------------------------------

    [Given("a primary channel that never responds")]
    public void GivenAPrimaryChannelThatNeverResponds()
    {
        _context.Set<IApprovalChannel>(new SlowFakeApprovalChannel("slow-primary"), KeyPrimaryChannel);
    }

    [Given(@"a secondary channel ""([^""]+)""")]
    public void GivenASecondaryChannel(string channelName)
    {
        var fake = new FakeApprovalChannel();
        var named = new NamedFakeApprovalChannel(channelName, fake);
        _context.Set<IApprovalChannel>(named, KeySecondaryChannel);
        _context.Set(fake, KeyFakeChannel);
    }

    [Given("a secondary channel that never responds")]
    public void GivenASecondaryChannelThatNeverResponds()
    {
        _context.Set<IApprovalChannel>(new SlowFakeApprovalChannel("slow-secondary"), KeySecondaryChannel);
    }

    [Given(@"an escalation chain configured with timeout (\d+) ms")]
    public void GivenAnEscalationChainConfiguredWithTimeout(int timeoutMs)
    {
        var primary = _context.Get<IApprovalChannel>(KeyPrimaryChannel);
        var secondary = _context.Get<IApprovalChannel>(KeySecondaryChannel);
        var composite = new CompositeApprovalChannel(primary, TimeSpan.FromMilliseconds(timeoutMs), secondary);
        _context.Set<IApprovalChannel>(composite, KeyCompositeChannel);
    }

    [Given(@"an escalation chain configured with timeout (\d+) ms for each hop")]
    public void GivenAnEscalationChainConfiguredWithTimeoutForEachHop(int timeoutMs)
    {
        var primary = _context.Get<IApprovalChannel>(KeyPrimaryChannel);
        var secondary = _context.Get<IApprovalChannel>(KeySecondaryChannel);

        // Wrap secondary with its own timeout so it also self-terminates.
        var secondaryWithTimeout = new EscalatingTimeoutChannel(
            secondary,
            TimeSpan.FromMilliseconds(timeoutMs),
            OnTimeoutAction.AutoReject,
            escalationTarget: null);

        var composite = new CompositeApprovalChannel(
            primary,
            TimeSpan.FromMilliseconds(timeoutMs),
            secondaryWithTimeout);

        _context.Set<IApprovalChannel>(composite, KeyCompositeChannel);
    }

    [When("an approval is requested")]
    public void WhenAnApprovalIsRequested()
    {
        var channel = _context.Get<IApprovalChannel>(KeyCompositeChannel);
        var request = new ApprovalRequestBuilder()
            .WithTitle("Escalation Test")
            .WithTimeout(TimeSpan.FromSeconds(30))
            .Build();

        _context.Set(request, KeyRequest);
        _context.Set(request.CorrelationId, KeyCorrelationId);

        var task = channel.RequestApprovalAsync(request);
        _context.Set(task, KeyApprovalTask);
    }

    [When("the primary times out")]
    public async Task WhenThePrimaryTimesOut()
    {
        // Brief pause to let the escalation deadline fire and secondary to start.
        await Task.Delay(200);
    }

    [When(@"the secondary channel receives an approval from ""([^""]+)""")]
    public void WhenTheSecondaryChannelReceivesAnApprovalFrom(string approverId)
    {
        var fake = _context.Get<FakeApprovalChannel>(KeyFakeChannel);
        var correlationId = _context.Get<string>(KeyCorrelationId);

        var records = new List<ApprovalRecord>
        {
            MakeVote(approverId, approved: true, channel: "email")
        };
        fake.Complete(correlationId, ApprovalResponse.ApprovedBy(records.AsReadOnly()));
    }

    [Then("the audit trail contains an escalation record from primary")]
    public async Task ThenAuditTrailContainsEscalationRecord()
    {
        var response = await GetOrAwaitResponseAsync();
        response.Approvals.Should().Contain(
            r => r.ApproverId == "system:escalated",
            "an escalation sentinel record must be present");
    }

    [Then(@"the audit trail contains an approval record from ""([^""]+)"" on channel ""([^""]+)""")]
    public async Task ThenAuditTrailContainsApprovalRecordFromOnChannel(string approverId, string channelName)
    {
        var response = await GetOrAwaitResponseAsync();
        response.Approvals.Should().Contain(
            r => r.ApproverId == approverId && r.Channel == channelName,
            $"approval record from '{approverId}' on channel '{channelName}' must be present");
    }

    // -------------------------------------------------------------------------
    // RestartResilience scenario steps
    // -------------------------------------------------------------------------

    [Given("an approval is requested and persisted")]
    public async Task GivenAnApprovalIsRequestedAndPersisted()
    {
        var store = new InMemoryApprovalStore();
        var fake = new FakeApprovalChannel();
        var svc = new PersistentApprovalService(fake, store);

        var request = new ApprovalRequestBuilder()
            .WithTitle("Restart Resilience Test")
            .WithTimeout(TimeSpan.FromSeconds(30))
            .Build();

        _context.Set(store, KeyStore);
        _context.Set(fake, KeyFakeChannel);
        _context.Set(svc, KeyService);
        _context.Set(request, KeyRequest);
        _context.Set(request.CorrelationId, KeyCorrelationId);

        var task = svc.RequestApprovalAsync(request);
        _context.Set(task, KeyApprovalTask);

        // Wait for SaveAsync to complete before simulating restart.
        await Task.Delay(50);
    }

    [Given("an approval is persisted with deadline already in the past and on-timeout action AutoReject")]
    public async Task GivenAnApprovalIsPersistedWithDeadlineInThePast()
    {
        var store = new InMemoryApprovalStore();

        var request = new ApprovalRequestBuilder()
            .WithTitle("Past Deadline Test")
            .WithTimeout(TimeSpan.FromMilliseconds(1))
            .Build();

        var now = DateTimeOffset.UtcNow;
        var pending = new PendingApproval(
            CorrelationId: request.CorrelationId,
            Request: request,
            PrimaryChannel: "fake",
            CreatedAt: now.AddSeconds(-60),
            DeadlineAt: now.AddSeconds(-30),
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject);

        await store.SaveAsync(pending);

        _context.Set(store, KeyStore);
        _context.Set(request, KeyRequest);
        _context.Set(request.CorrelationId, KeyCorrelationId);
    }

    [When("the host stops and starts again")]
    public async Task WhenTheHostStopsAndStartsAgain()
    {
        var store = _context.Get<InMemoryApprovalStore>(KeyStore);
        var fake = new FakeApprovalChannel();
        var newSvc = new PersistentApprovalService(fake, store);

        _context.Set(fake, KeyFakeChannel);
        _context.Set(newSvc, KeyRehydratedService);

        var pending = await store.ListPendingAsync();
        foreach (var p in pending)
            newSvc.Rehydrate(p);

        var correlationId = _context.Get<string>(KeyCorrelationId);
        var waitTask = newSvc.WaitForCompletionAsync(correlationId);
        _context.Set(waitTask, KeyRehydratedTask);
    }

    [When("the host starts")]
    public async Task WhenTheHostStarts()
    {
        var store = _context.Get<InMemoryApprovalStore>(KeyStore);
        var fake = new FakeApprovalChannel();
        var newSvc = new PersistentApprovalService(fake, store);

        _context.Set(fake, KeyFakeChannel);
        _context.Set(newSvc, KeyRehydratedService);

        var pending = await store.ListPendingAsync();
        foreach (var p in pending)
            newSvc.Rehydrate(p);

        var correlationId = _context.Get<string>(KeyCorrelationId);
        var waitTask = newSvc.WaitForCompletionAsync(correlationId);
        _context.Set(waitTask, KeyRehydratedTask);
    }

    [When(@"approver ""([^""]+)"" approves via the store")]
    public async Task WhenApproverApprovesViaTheStore(string approverId)
    {
        var newSvc = _context.Get<PersistentApprovalService>(KeyRehydratedService);
        var correlationId = _context.Get<string>(KeyCorrelationId);
        var vote = MakeVote(approverId, approved: true);
        await newSvc.ResolveExternalAsync(correlationId, vote);
    }

    [Then("the rehydrated approval response is Approved")]
    public async Task ThenTheRehydratedApprovalResponseIsApproved()
    {
        var waitTask = _context.Get<Task<ApprovalResponse>>(KeyRehydratedTask);
        var response = await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
        response.Outcome.Should().Be(ApprovalOutcome.Approved);
    }

    [Then("the persisted approval completes with outcome TimedOut")]
    public async Task ThenThePersistedApprovalCompletesWithOutcomeTimedOut()
    {
        var waitTask = _context.Get<Task<ApprovalResponse>>(KeyRehydratedTask);
        var response = await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
        response.Outcome.Should().Be(ApprovalOutcome.TimedOut);
    }

    // -------------------------------------------------------------------------
    // AllowedRoles scenario steps
    // -------------------------------------------------------------------------

    [Given(@"a pending approval restricted to roles ""([^""]+)"" and ""([^""]+)""")]
    public void GivenPendingApprovalRestrictedToTwoRoles(string role1, string role2)
    {
        var request = new ApprovalRequestBuilder()
            .WithTitle("AllowedRoles Test")
            .WithTimeout(TimeSpan.FromSeconds(30))
            .AllowedFor(role1, role2)
            .Build();

        SetupPersistentService(request);
    }

    [Given(@"a pending approval restricted to roles ""([^""]+)""")]
    public void GivenPendingApprovalRestrictedToRole(string role)
    {
        var request = new ApprovalRequestBuilder()
            .WithTitle("AllowedRoles Test")
            .WithTimeout(TimeSpan.FromSeconds(30))
            .AllowedFor(role)
            .Build();

        SetupPersistentService(request);
    }

    [Given("a pending approval with no role restrictions")]
    public void GivenPendingApprovalWithNoRoleRestrictions()
    {
        var request = new ApprovalRequestBuilder()
            .WithTitle("No Roles Test")
            .WithTimeout(TimeSpan.FromSeconds(30))
            .Build();

        SetupPersistentService(request);
    }

    [When(@"approver ""([^""]+)"" attempts to approve")]
    public async Task WhenApproverAttemptsToApprove(string approverId)
    {
        try
        {
            await SubmitVoteAsync(approverId, approved: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            _context.Set<Exception>(ex, KeyLastVoteException);
        }
    }

    [Then("the vote is rejected as unauthorized")]
    public void ThenTheVoteIsRejectedAsUnauthorized()
    {
        _context.TryGetValue(KeyLastVoteException, out Exception? ex);
        ex.Should().NotBeNull("an UnauthorizedAccessException should have been thrown");
        ex.Should().BeOfType<UnauthorizedAccessException>();
    }

    // -------------------------------------------------------------------------
    // AuditTrail scenario steps
    // -------------------------------------------------------------------------

    [When(@"approver ""([^""]+)"" with display name ""([^""]+)"" approves with comment ""([^""]+)""")]
    public async Task WhenApproverWithDisplayNameApprovesWithComment(
        string approverId, string displayName, string comment)
    {
        await SubmitVoteAsync(approverId, approved: true,
            displayName: displayName, comment: comment);
    }

    [Then(@"the record for ""([^""]+)"" has display name ""([^""]+)""")]
    public async Task ThenRecordHasDisplayName(string approverId, string expectedDisplayName)
    {
        var response = await GetOrAwaitResponseAsync();
        var record = response.Approvals.FirstOrDefault(r => r.ApproverId == approverId);
        record.Should().NotBeNull($"a record for '{approverId}' should exist");
        record!.ApproverDisplayName.Should().Be(expectedDisplayName);
    }

    [Then(@"the record for ""([^""]+)"" has comment ""([^""]+)""")]
    public async Task ThenRecordHasComment(string approverId, string expectedComment)
    {
        var response = await GetOrAwaitResponseAsync();
        var record = response.Approvals.FirstOrDefault(r => r.ApproverId == approverId);
        record.Should().NotBeNull($"a record for '{approverId}' should exist");
        record!.Comment.Should().Be(expectedComment);
    }

    [Then(@"every record has a timestamp within the last (\d+) seconds")]
    public async Task ThenEveryRecordHasTimestampWithinLastSeconds(int seconds)
    {
        var response = await GetOrAwaitResponseAsync();
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-seconds);
        foreach (var record in response.Approvals)
        {
            record.Timestamp.Should().BeOnOrAfter(cutoff,
                $"record for '{record.ApproverId}' timestamp should be recent");
        }
    }

    [Then(@"every record has channel ""([^""]+)""")]
    public async Task ThenEveryRecordHasChannel(string channelName)
    {
        var response = await GetOrAwaitResponseAsync();
        foreach (var record in response.Approvals)
        {
            record.Channel.Should().Be(channelName,
                $"record for '{record.ApproverId}' should have channel '{channelName}'");
        }
    }
}
