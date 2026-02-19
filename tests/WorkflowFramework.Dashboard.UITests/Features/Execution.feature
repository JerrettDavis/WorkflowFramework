@UI
Feature: Workflow Execution
  As a user I can run workflows and monitor execution in real-time

  @screenshot:execution-panel @screenshot:node-status-colors
  Scenario: Run a valid workflow
    Given the dashboard is running
    And I have a valid workflow with 3 steps
    When I run the workflow
    Then the execution panel should appear
    And I should see step status updates

  @screenshot:run-history @screenshot:overview
  Scenario: View run history
    Given the dashboard is running
    And I have completed workflow runs
    When I navigate to run history
    Then I should see past runs with status and duration
