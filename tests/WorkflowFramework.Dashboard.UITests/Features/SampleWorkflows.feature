@UI
Feature: Sample Workflows
  Pre-loaded sample workflows are available and fully configured

  Scenario: Sample workflows appear in workflow list
    Given the dashboard is running
    When I open the workflow list dialog
    Then I should see at least 10 sample workflows
    And I should see "Hello World" in the list
    And I should see "TaskStream" in the list
    And I should see "Quick Transcript" in the list

  Scenario: Open a sample workflow and see configured steps
    Given the dashboard is running
    When I open the workflow list dialog
    And I open the "Hello World" workflow
    Then the canvas should have nodes
    And the step list should show steps
    And the workflow name should be "Hello World"

  Scenario: Open AI workflow and verify provider config
    Given the dashboard is running
    When I open the workflow list dialog
    And I open the "Quick Transcript" workflow
    And I click on a node of type "LlmCallStep"
    Then the properties panel should show "AI Provider" configuration
    And the provider field should have a value

  Scenario: Open HTTP workflow and verify URL config
    Given the dashboard is running
    When I open the workflow list dialog
    And I open the "HTTP API Orchestration" workflow
    And I click on a node of type "HttpStep"
    Then the properties panel should show "URL" configuration
    And the url field should not be empty
