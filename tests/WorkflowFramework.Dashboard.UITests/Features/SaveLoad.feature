@UI
Feature: Save and Load Workflows
  Workflows save and load with all configuration intact

  Scenario: Save a new workflow
    Given the dashboard is running
    And I have designed a workflow with 2 action steps
    When I save the workflow as "My Test Workflow"
    Then I should see a success toast
    And the workflow should appear in the workflow list

  Scenario: Save preserves step configuration
    Given the dashboard is running
    And I open the "HTTP API Orchestration" sample workflow
    And I note the configuration of the first HttpStep
    When I save the workflow
    And I reload the page
    And I open the saved workflow
    And I select the first HttpStep
    Then the URL should match the original configuration
    And the method should match the original configuration

  Scenario: Save and reopen AI workflow preserves prompts
    Given the dashboard is running
    And I open the "Quick Transcript" sample workflow
    When I save the workflow
    And I reload the page
    And I open the saved workflow
    And I select the LlmCallStep
    Then the prompt textarea should contain text
    And the provider should be set

  Scenario: Duplicate a workflow
    Given the dashboard is running
    And I have a saved workflow "Original Workflow"
    When I duplicate the workflow
    Then a new workflow "Original Workflow (Copy)" should exist
