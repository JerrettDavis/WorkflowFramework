@UI
Feature: Workflow Execution Deep
  Workflows execute with real step processing and live progress

  Scenario: Run Hello World workflow end-to-end
    Given the dashboard is running
    And I open the "Hello World" sample workflow
    When I save and run the workflow
    Then the execution panel should show "Running"
    And I should see step progress updates
    And the run should complete with status "Completed"

  Scenario: Run HTTP workflow makes real requests
    Given the dashboard is running
    And I open the "HTTP API Orchestration" sample workflow
    When I save and run the workflow
    Then I should see step "FetchUserData" complete
    And I should see step "FetchUserPosts" complete
    And the run should complete with status "Completed"

  Scenario: Cancel a running workflow
    Given the dashboard is running
    And I have a workflow with a Delay step of 30000ms
    When I save and run the workflow
    And the workflow is running
    When I cancel the run
    Then the run should show status "Cancelled"

  Scenario: Failed step shows error in output
    Given the dashboard is running
    And I have a workflow with an HttpStep pointing to an invalid URL
    When I save and run the workflow
    Then I should see a step failure in the output panel
    And the run should complete with status "Failed"

  Scenario: View run in history after completion
    Given the dashboard is running
    And I open the "Hello World" sample workflow
    When I save and run the workflow
    And the run completes
    Then the run should appear in the runs list via API
