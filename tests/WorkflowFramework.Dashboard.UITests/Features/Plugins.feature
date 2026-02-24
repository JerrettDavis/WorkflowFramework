@UI
Feature: Plugins
  Plugin system and step type extensions

  Scenario: Plugin list endpoint returns loaded plugins
    Given the dashboard is running
    When I request the plugins API
    Then I should see the email plugin
    And the email plugin should have a SendEmail step type

  Scenario: Plugin step types appear in step registry
    Given the dashboard is running
    When I request the step types API
    Then the step types should include SendEmail
    And the SendEmail step should be in the Communication category

  Scenario: Webhook trigger endpoint exists
    Given the dashboard is running
    And a workflow exists
    When I trigger the workflow via webhook API
    Then I should receive a webhook response with a run ID
