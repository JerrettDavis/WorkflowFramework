@UI
Feature: Keyboard Shortcuts
  As a user I can use keyboard shortcuts for common actions

  @screenshot:help-modal
  Scenario: Open shortcuts help
    Given the dashboard is running
    When I press "F1"
    Then I should see the keyboard shortcuts modal

  Scenario: Save with Ctrl+S
    Given the dashboard is running
    And I have a dirty workflow
    When I press Ctrl+S
    Then the workflow should be saved
