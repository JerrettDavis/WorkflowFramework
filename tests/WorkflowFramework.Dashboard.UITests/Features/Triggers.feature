@UI
Feature: Triggers
  Workflow trigger configuration

  @screenshot:trigger-panel
  Scenario: Triggers tab is visible in designer
    Given the dashboard is running
    When I navigate to the designer
    And I click the triggers tab
    Then I should see the trigger panel
    And the trigger panel should show an empty state

  @screenshot:trigger-add-form
  Scenario: Add trigger form shows available types
    Given the dashboard is running
    When I navigate to the designer
    And I click the triggers tab
    And I click the add trigger button
    Then I should see the trigger type selector
    And the selector should contain schedule trigger
    And the selector should contain webhook trigger
    And the selector should contain file watcher trigger
    And the selector should contain audio input trigger
    And the selector should contain message queue trigger
    And the selector should contain manual trigger

  @screenshot:trigger-configured
  Scenario: Add a schedule trigger
    Given the dashboard is running
    And a workflow exists
    When I navigate to the designer
    And I open the workflow
    And I click the triggers tab
    And I click the add trigger button
    And I select trigger type "schedule"
    And I confirm adding the trigger
    Then I should see a trigger item in the panel

  Scenario: Remove a trigger
    Given the dashboard is running
    And a workflow exists
    When I navigate to the designer
    And I open the workflow
    And I click the triggers tab
    And I click the add trigger button
    And I select trigger type "manual"
    And I confirm adding the trigger
    Then I should see a trigger item in the panel
    When I click remove on the trigger
    Then the trigger panel should show an empty state

  Scenario: Trigger types API returns all types
    Given the dashboard is running
    When I request the trigger types API
    Then I should receive 6 trigger types
    And each trigger type should have a config schema
