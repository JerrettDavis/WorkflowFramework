@UI
Feature: Import Export
  Workflow import and export functionality

  @screenshot:toolbar-import-export
  Scenario: Import and export buttons are visible
    Given the dashboard is running
    When I navigate to the designer
    Then I should see the import button in the toolbar

  Scenario: Export workflow via API
    Given the dashboard is running
    And a workflow exists
    When I export the workflow via API
    Then the export should contain the workflow definition
    And the export should have a format version

  Scenario: Import workflow via API
    Given the dashboard is running
    When I import a workflow via API
    Then the imported workflow should be accessible
    And the imported workflow should have the correct name

  Scenario: Export and re-import round trip
    Given the dashboard is running
    And a workflow exists
    When I export the workflow via API
    And I import the exported workflow via API
    Then both workflows should have the same definition
