using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class RunAssistantSteps
{
    private const string BrowserErrorKey = "BrowserErrors";
    private readonly ScenarioContext _context;

    public RunAssistantSteps(ScenarioContext context)
    {
        _context = context;
    }

    private IPage Page => _context.Get<IPage>();

    [Given("the browser voice recorder is mocked")]
    public async Task GivenTheBrowserVoiceRecorderIsMocked()
    {
        var browserErrors = new List<string>();
        _context.Set(browserErrors, BrowserErrorKey);

        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
                browserErrors.Add(msg.Text);
        };

        Page.PageError += (_, message) => browserErrors.Add(message);

        await Page.AddInitScriptAsync(
            """
            (() => {
              window.__uiRuntimeErrors = [];
              window.__uiConsoleErrors = [];
              window.addEventListener('error', e => window.__uiRuntimeErrors.push((e && e.message) ? e.message : 'window.error'));
              window.addEventListener('unhandledrejection', e => {
                const reason = e && e.reason ? (e.reason.message || String(e.reason)) : 'unhandledrejection';
                window.__uiRuntimeErrors.push(reason);
              });
              const originalConsoleError = console.error.bind(console);
              console.error = (...args) => {
                try {
                  window.__uiConsoleErrors.push(args.map(a => String(a)).join(' '));
                } catch { }
                originalConsoleError(...args);
              };

              const mockRecorder = {
                _recording: false,
                listInputDevices: async () => [{ deviceId: 'mock-mic', label: 'Mock Microphone' }],
                supportsSpeechRecognition: () => true,
                startRecording: async () => {
                  mockRecorder._recording = true;
                  return { isRecording: true, speechRecognition: true };
                },
                stopRecording: async () => {
                  mockRecorder._recording = false;
                  return {
                    fileName: 'mock.wav',
                    mimeType: 'audio/wav',
                    size: 12,
                    base64: 'QUJD',
                    previewUrl: 'data:audio/wav;base64,QUJD',
                    liveTranscript: 'mock transcript text'
                  };
                },
                isRecording: () => mockRecorder._recording,
                clearPreview: () => { },
                dispose: () => { mockRecorder._recording = false; }
              };

              Object.defineProperty(window, 'voiceRecorder', {
                configurable: true,
                get() { return mockRecorder; },
                set(_) { /* ignore runtime overrides from voice-recorder.js */ }
              });
            })();
            """);
    }

    [Then("the run assistant should open with recording controls")]
    public async Task ThenTheRunAssistantShouldOpenWithRecordingControls()
    {
        await Page.WaitForSelectorAsync("[data-testid='tab-run-assistant']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
        await Page.Locator("[data-testid='tab-run-assistant']").ClickAsync();

        await Page.WaitForSelectorAsync("[data-testid='run-assistant-content']",
            new PageWaitForSelectorOptions { Timeout = 15_000 });

        var runAssistant = Page.Locator("[data-testid='run-assistant-content']");
        (await runAssistant.TextContentAsync()).Should().Contain("Guided Run Setup");

        var hero = runAssistant.Locator("[data-testid='run-assistant-hero']");
        await hero.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });

        var progressFill = runAssistant.Locator("[data-testid='run-assistant-progress-fill']").First;
        await progressFill.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000, State = WaitForSelectorState.Attached });
        (await progressFill.GetAttributeAsync("style")).Should().Contain("width:");

        var startRecordingButton = runAssistant.Locator("[data-testid='btn-run-assistant-record']").First;
        await startRecordingButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        (await startRecordingButton.IsEnabledAsync()).Should().BeTrue();
    }

    [When("I start recording in the run assistant")]
    public async Task WhenIStartRecordingInTheRunAssistant()
    {
        var runAssistant = Page.Locator("[data-testid='run-assistant-content']");
        var startRecordingButton = runAssistant.Locator("[data-testid='btn-run-assistant-record']").First;
        await startRecordingButton.ClickAsync();

        var stopRecordingButton = runAssistant.Locator("[data-testid='btn-run-assistant-record']").First;
        await stopRecordingButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        (await stopRecordingButton.TextContentAsync()).Should().Contain("Stop Recording");
    }

    [When("I stop recording in the run assistant")]
    public async Task WhenIStopRecordingInTheRunAssistant()
    {
        var runAssistant = Page.Locator("[data-testid='run-assistant-content']");
        var stopRecordingButton = runAssistant.Locator("[data-testid='btn-run-assistant-record']").First;
        await stopRecordingButton.ClickAsync();

        await runAssistant.Locator("audio").First
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
    }

    [Then("the run assistant should show captured audio")]
    public async Task ThenTheRunAssistantShouldShowCapturedAudio()
    {
        var runAssistant = Page.Locator("[data-testid='run-assistant-content']");
        var text = await runAssistant.TextContentAsync();
        text.Should().Contain("mock.wav");

        var audioPreview = runAssistant.Locator("[data-testid='run-assistant-audio-preview'] audio").First;
        (await audioPreview.IsVisibleAsync()).Should().BeTrue();
    }

    [Then("the run assistant should expose multiple interactive tasks")]
    public async Task ThenTheRunAssistantShouldExposeMultipleInteractiveTasks()
    {
        var runAssistant = Page.Locator("[data-testid='run-assistant-content']");
        var taskStrip = runAssistant.Locator("[data-testid='run-assistant-task-strip']");
        await taskStrip.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });

        var taskPills = runAssistant.Locator("[data-testid='run-assistant-task-pill']");
        var taskCount = await taskPills.CountAsync();
        taskCount.Should().BeGreaterThanOrEqualTo(4, "Blog Interview should expose a multi-step guided assistant.");
    }

    [Then("the run assistant should allow continuing to the next step")]
    public async Task ThenTheRunAssistantShouldAllowContinuingToTheNextStep()
    {
        var runAssistant = Page.Locator("[data-testid='run-assistant-content']");
        var nextButtons = runAssistant.Locator("[data-testid='btn-run-assistant-next']");
        if (await nextButtons.CountAsync() > 0)
        {
            var nextButton = nextButtons.First;
            await nextButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
            (await nextButton.IsEnabledAsync()).Should().BeTrue();
            var titleBefore = (await runAssistant.Locator("[data-testid='run-assistant-task-title']").TextContentAsync())?.Trim();
            await nextButton.ClickAsync();

            await Page.WaitForTimeoutAsync(250);
            var titleAfter = (await runAssistant.Locator("[data-testid='run-assistant-task-title']").TextContentAsync())?.Trim();
            titleAfter.Should().NotBe(titleBefore, "moving forward should update the active task view");
        }

        var startWorkflowButtons = runAssistant.Locator("[data-testid='btn-run-assistant-start-workflow']");
        if (await startWorkflowButtons.CountAsync() > 0)
        {
            var startWorkflowButton = startWorkflowButtons.First;
            await startWorkflowButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
            (await startWorkflowButton.IsEnabledAsync()).Should().BeTrue();
            await startWorkflowButton.ClickAsync();

            var statusBadge = Page.Locator("[data-testid='run-status-badge']");
            await statusBadge.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

            var statusText = await statusBadge.TextContentAsync() ?? "";
            statusText.Should().MatchRegex("(?i)(running|completed|failed|cancelled)");

            var narrative = Page.Locator("[data-testid='run-narrative']");
            await narrative.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
            return;
        }

        var remainingNextButtons = runAssistant.Locator("[data-testid='btn-run-assistant-next']");
        (await remainingNextButtons.CountAsync()).Should().BeGreaterThan(0, "workflow should still expose a path to continue interactive tasks");
        (await remainingNextButtons.First.IsEnabledAsync()).Should().BeTrue();
    }

    [When("I complete the run assistant flow and start the workflow")]
    public async Task WhenICompleteTheRunAssistantFlowAndStartTheWorkflow()
    {
        var runAssistant = Page.Locator("[data-testid='run-assistant-content']");
        const int maxTransitions = 18;
        for (var i = 0; i < maxTransitions; i++)
        {
            var startButtons = runAssistant.Locator("[data-testid='btn-run-assistant-start-workflow']");
            if (await startButtons.CountAsync() > 0)
            {
                var startButton = startButtons.First;
                await startButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
                await EnsureCurrentTaskCanAdvanceAsync(runAssistant);
                (await startButton.IsEnabledAsync()).Should().BeTrue("final run assistant task should be satisfied before launch");
                await startButton.ClickAsync();

                var statusBadge = Page.Locator("[data-testid='run-status-badge']");
                await statusBadge.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
                return;
            }

            var nextButtons = runAssistant.Locator("[data-testid='btn-run-assistant-next']");
            (await nextButtons.CountAsync()).Should().BeGreaterThan(0, "assistant should provide Next or Start Workflow actions");
            var nextButton = nextButtons.First;
            await nextButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });

            await EnsureCurrentTaskCanAdvanceAsync(runAssistant);
            (await nextButton.IsEnabledAsync()).Should().BeTrue("current guided task should be completable");

            var titleBefore = (await runAssistant.Locator("[data-testid='run-assistant-task-title']").TextContentAsync())?.Trim();
            await nextButton.ClickAsync();
            await Page.WaitForTimeoutAsync(250);

            var titleAfter = (await runAssistant.Locator("[data-testid='run-assistant-task-title']").TextContentAsync())?.Trim();
            if (!string.IsNullOrWhiteSpace(titleBefore) && !string.IsNullOrWhiteSpace(titleAfter))
            {
                titleAfter.Should().NotBe(titleBefore, "moving next should advance to a different guided task");
            }
        }

        throw new TimeoutException("Run assistant did not reach the Start Workflow action in time.");
    }

    [Then("the output feed should show detailed execution telemetry")]
    public async Task ThenTheOutputFeedShouldShowDetailedExecutionTelemetry()
    {
        var outputTab = Page.Locator("[data-testid='tab-output']");
        await outputTab.ClickAsync();

        var output = Page.Locator("[data-testid='output-content']");
        await output.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var statusBadge = Page.Locator("[data-testid='run-status-badge']");
        await statusBadge.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        var statusText = await WaitForTextAsync(
            statusBadge,
            text => text.Contains("Running", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Completed", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Failed", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Cancelled", StringComparison.OrdinalIgnoreCase),
            20_000);
        statusText.Should().MatchRegex("(?i)(running|completed|failed|cancelled)");

        var feedMode = Page.Locator("[data-testid='run-feed-mode']");
        await feedMode.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        var feedText = await feedMode.TextContentAsync() ?? string.Empty;
        feedText.Should().Contain("Feed:");
        feedText.Should().MatchRegex("(?i)(realtime|polling fallback)");

        var knownSteps = await WaitForMetricAsync(
            Page.Locator("[data-testid='run-metric-known']"),
            value => value > 0,
            15_000);
        knownSteps.Should().BeGreaterThan(0);

        var runningOrSettled = await WaitForAnyMetricAsync(
            [
                Page.Locator("[data-testid='run-metric-running']"),
                Page.Locator("[data-testid='run-metric-completed']"),
                Page.Locator("[data-testid='run-metric-failed']")
            ],
            value => value > 0,
            15_000);
        runningOrSettled.Should().BeTrue("execution metrics should react once run tracking begins");

        var narrative = Page.Locator("[data-testid='run-narrative']");
        await narrative.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        var narrativeText = await narrative.TextContentAsync() ?? string.Empty;
        narrativeText.Should().Contain("What's Happening Now");
        narrativeText.Should().NotContain("Start a workflow run to see detailed execution telemetry.");

        var timelineRows = Page.Locator("[data-testid='run-event-row']");
        await timelineRows.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        (await timelineRows.CountAsync()).Should().BeGreaterThan(0, "timeline events should explain run progress");

        var rawLog = Page.Locator("details.run-raw-log");
        await rawLog.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        var rawLogText = await rawLog.TextContentAsync() ?? string.Empty;
        rawLogText.Should().MatchRegex("(?i)(Run requested|Run started|Run status)");
    }

    [Then("the browser should not report run assistant interop errors")]
    public async Task ThenTheBrowserShouldNotReportRunAssistantInteropErrors()
    {
        var errors = _context.TryGetValue<List<string>>(BrowserErrorKey, out var captured)
            ? captured
            : [];

        var runtimeErrors = await Page.EvaluateAsync<string[]>("() => window.__uiRuntimeErrors || []");
        var consoleErrors = await Page.EvaluateAsync<string[]>("() => window.__uiConsoleErrors || []");

        var allErrors = new List<string>();
        allErrors.AddRange(errors);
        allErrors.AddRange(runtimeErrors);
        allErrors.AddRange(consoleErrors);

        allErrors.Should().NotContain(err => err.Contains("OnSelectionChanged", StringComparison.OrdinalIgnoreCase));
        allErrors.Should().NotContain(err => err.Contains("exception invoking", StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureCurrentTaskCanAdvanceAsync(ILocator runAssistant)
    {
        var addQuestionButton = runAssistant.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add question" });
        if (await addQuestionButton.CountAsync() > 0)
        {
            var existingQuestionInputs = runAssistant.Locator("input.run-assistant-input");
            if (await existingQuestionInputs.CountAsync() == 0)
            {
                await addQuestionButton.First.ClickAsync();
            }
        }

        await FillEmptyInputsAsync(runAssistant.Locator("input.run-assistant-input"), "UI test question");
        await FillEmptyInputsAsync(runAssistant.Locator("textarea"), "UI test response");

        var requirement = (await runAssistant.Locator(".run-assistant-requirement").TextContentAsync()) ?? string.Empty;
        var needsRecording = requirement.Contains("record", StringComparison.OrdinalIgnoreCase);
        var audioPreview = runAssistant.Locator("[data-testid='run-assistant-audio-preview']");
        var recordButton = runAssistant.Locator("[data-testid='btn-run-assistant-record']");

        if (needsRecording && await audioPreview.CountAsync() == 0 && await recordButton.CountAsync() > 0)
        {
            await recordButton.First.ClickAsync();
            await Page.WaitForTimeoutAsync(200);
            await recordButton.First.ClickAsync();
            await audioPreview.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        }
    }

    private static async Task FillEmptyInputsAsync(ILocator inputs, string fillValue)
    {
        var count = await inputs.CountAsync();
        for (var i = 0; i < count; i++)
        {
            var input = inputs.Nth(i);
            if (!await input.IsVisibleAsync())
                continue;

            var existingValue = await input.InputValueAsync();
            if (string.IsNullOrWhiteSpace(existingValue))
            {
                await input.FillAsync(fillValue);
            }
        }
    }

    private async Task<int> WaitForMetricAsync(ILocator metric, Func<int, bool> predicate, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var value = await ReadMetricValueAsync(metric);
            if (predicate(value))
                return value;

            await Page.WaitForTimeoutAsync(250);
        }

        return await ReadMetricValueAsync(metric);
    }

    private async Task<bool> WaitForAnyMetricAsync(IEnumerable<ILocator> metrics, Func<int, bool> predicate, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var metric in metrics)
            {
                var value = await ReadMetricValueAsync(metric);
                if (predicate(value))
                    return true;
            }

            await Page.WaitForTimeoutAsync(250);
        }

        return false;
    }

    private async Task<string> WaitForTextAsync(ILocator locator, Func<string, bool> predicate, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var text = (await locator.TextContentAsync()) ?? string.Empty;
            if (predicate(text))
                return text;

            await Page.WaitForTimeoutAsync(250);
        }

        return (await locator.TextContentAsync()) ?? string.Empty;
    }

    private static async Task<int> ReadMetricValueAsync(ILocator metric)
    {
        var raw = (await metric.TextContentAsync())?.Trim();
        return int.TryParse(raw, out var parsed) ? parsed : 0;
    }
}
