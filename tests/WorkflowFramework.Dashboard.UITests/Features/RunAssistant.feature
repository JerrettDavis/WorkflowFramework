@UI
Feature: Run Assistant
  Audio workflows should provide interactive recording UX and stay free of interop exceptions

  Scenario: Record audio in run assistant for Quick Transcript workflow
    Given the browser voice recorder is mocked
    And the dashboard is running
    And I open the "Quick Transcript" sample workflow
    When I run the workflow
    Then the run assistant should open with recording controls
    When I start recording in the run assistant
    And I stop recording in the run assistant
    Then the run assistant should show captured audio
    And the run assistant should allow continuing to the next step
    And the browser should not report run assistant interop errors
