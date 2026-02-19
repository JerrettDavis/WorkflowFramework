@UI
Feature: Workflow Management
  As a user I can create, save, open, and manage workflows

  @screenshot:new-workflow-dialog
  Scenario: Create a new workflow
    Given the dashboard is running
    When I click the New button
    Then I should see a fresh empty canvas
    And the workflow name should be "Untitled Workflow"

  @screenshot:open-workflow-dialog
  Scenario: Save and reopen a workflow
    Given the dashboard is running
    And I have created a workflow named "Test Workflow"
    When I save the workflow
    And I click Open
    Then I should see "Test Workflow" in the workflow list

  Scenario: Export workflow as JSON
    Given the dashboard is running
    And I have a saved workflow
    When I click Export JSON
    Then a JSON file should be downloaded
