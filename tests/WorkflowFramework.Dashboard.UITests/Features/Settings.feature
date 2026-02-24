@UI
Feature: Settings Management
  As a user I can configure AI providers and execution settings

  Scenario: Navigate to settings page
    Given the dashboard is running
    When I click the Settings nav link
    Then I should see the settings page
    And I should see the AI Providers section
    And I should see the Execution section

  Scenario: Configure Ollama provider
    Given the dashboard is running
    And I am on the settings page
    When I set the Ollama URL to "http://localhost:11434"
    And I click Save Settings
    Then I should see a success toast

  Scenario: Select default provider and model
    Given the dashboard is running
    And I am on the settings page
    When I select "ollama" as the default provider
    Then the model dropdown should be populated
    When I select a model
    And I click Save Settings
    Then the settings should persist

  Scenario: Test Ollama connection
    Given the dashboard is running
    And I am on the settings page
    When I click Test Connection
    Then I should see a connection test result
