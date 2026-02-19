@UI
Feature: Templates
  As a user I can create workflows from pre-built templates

  @screenshot:template-browser
  Scenario: Browse templates
    Given the dashboard is running
    When I open the template browser
    Then I should see template categories
    And I should see templates with difficulty badges

  Scenario: Create workflow from template
    Given the dashboard is running
    When I open the template browser
    And I select a template
    And I click "Use This Template"
    Then a new workflow should be created with the template steps
