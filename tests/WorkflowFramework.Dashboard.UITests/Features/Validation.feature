@UI
Feature: Workflow Validation
  As a user I get validation feedback before running workflows

  @screenshot:validation-badge @screenshot:validation-panel
  Scenario: Validate empty workflow
    Given the dashboard is running
    And I have an empty workflow
    When I click Validate
    Then I should see validation errors
    And the toolbar should show an error badge

  Scenario: Auto-validate before run
    Given the dashboard is running
    And I have a workflow with errors
    When I click Run
    Then the run should be blocked
    And validation errors should be displayed
