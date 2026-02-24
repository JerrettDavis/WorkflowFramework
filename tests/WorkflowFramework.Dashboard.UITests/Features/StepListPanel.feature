@UI
Feature: Step List Panel
  The bottom Steps tab shows a tree of all workflow steps

  Scenario: Step list shows workflow steps
    Given the dashboard is running
    And I open the "Order Processing Pipeline" sample workflow
    When I click the Steps tab
    Then I should see all workflow steps listed
    And the step types should be labeled

  Scenario: Click step in list focuses canvas
    Given the dashboard is running
    And I open a sample workflow with multiple steps
    When I click the Steps tab
    And I click a step in the step list
    Then the canvas should focus on that step
    And the properties panel should show that step's config

  Scenario: Step list updates after adding a step
    Given the dashboard is running
    And I have an empty workflow
    When I drag a step from the palette to the canvas
    And I click the Steps tab
    Then the step list should show the new step
