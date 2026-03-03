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
}
