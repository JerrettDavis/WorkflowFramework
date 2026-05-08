@UI
Feature: Workflow Designer
  As a user I can visually design workflows using the drag-and-drop designer

  @screenshot:designer-overview
  Scenario: View empty designer on launch
    Given the dashboard is running
    When I navigate to the designer
    Then I should see the step palette on the left
    And I should see an empty canvas
    And I should see the properties panel on the right

  @screenshot:step-palette
  Scenario: Search step palette
    Given the dashboard is running
    When I navigate to the designer
    And I type "conditional" in the step search
    Then I should see filtered steps containing "conditional"

  @screenshot:properties-panel
  Scenario: Select a node and view properties
    Given the dashboard is running
    And I have a workflow with an action step
    When I click on the action step node
    Then the properties panel should show the step configuration

  Scenario: SubWorkflow step shows saved workflow suggestions
    Given the dashboard is running
    And I have a workflow with a SubWorkflow step
    When I select the SubWorkflow step
    Then the properties panel should show saved workflow suggestions including "Child Flow"
    When I choose saved workflow "Child Flow"
    Then the SubWorkflow reference should be "Child Flow"

  @screenshot:toolbar
  Scenario: View toolbar
    Given the dashboard is running
    When I navigate to the designer
    Then the toolbar should show save, run, validate, and settings buttons
