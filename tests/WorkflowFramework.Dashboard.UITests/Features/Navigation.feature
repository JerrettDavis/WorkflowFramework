@UI
Feature: Navigation
  App-level navigation between pages

  Scenario: Navigate between Designer and Settings
    Given the dashboard is running
    When I click the Settings nav link
    Then I should be on the settings page
    When I click the Designer nav link
    Then I should be on the designer page

  Scenario: Back button from settings
    Given the dashboard is running
    And I am on the settings page
    When I click the back arrow
    Then I should be on the designer page
