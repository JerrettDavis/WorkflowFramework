@UI
Feature: Run Assistant
  Audio workflows should provide interactive recording UX and stay free of interop exceptions

  Scenario: Blog Interview workflow opens guided recording tasks
    Given the browser voice recorder is mocked
    And the dashboard is running
    And I open the "Blog Interview" sample workflow
    When I run the workflow
    Then the run assistant should open with recording controls
    And the run assistant should expose multiple interactive tasks
    And the browser should not report run assistant interop errors

  Scenario: Record audio in run assistant for Quick Transcript workflow
    Given the browser voice recorder is mocked
    And the dashboard is running
    And I open the "Quick Transcript" sample workflow
    When I run the workflow
    Then the run assistant should open with recording controls
    When I start recording in the run assistant
    And I stop recording in the run assistant
    Then the run assistant should show captured audio
    When I complete the run assistant flow and start the workflow
    Then the output feed should show detailed execution telemetry
    And the browser should not report run assistant interop errors
