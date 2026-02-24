@UI
Feature: Properties Panel
  Step properties show appropriate controls based on step type

  Scenario: Action step shows expression field
    Given the dashboard is running
    And I have a workflow with an Action step
    When I select the Action step
    Then the properties panel should show "Expression" field
    And the field should be a text input

  Scenario: LLM step shows provider dropdown and prompt textarea
    Given the dashboard is running
    And I have a workflow with a LlmCallStep
    When I select the LlmCallStep
    Then the properties panel should show a provider dropdown
    And the properties panel should show a model dropdown
    And the properties panel should show a prompt textarea
    And the properties panel should show a temperature slider

  Scenario: HTTP step shows method dropdown and URL field
    Given the dashboard is running
    And I have a workflow with an HttpStep
    When I select the HttpStep
    Then the properties panel should show a method dropdown with options "GET,POST,PUT,DELETE,PATCH"
    And the properties panel should show a URL text field
    And the properties panel should show a headers JSON editor
    And the properties panel should show a body JSON editor

  Scenario: Human task step shows priority dropdown
    Given the dashboard is running
    And I have a workflow with a HumanTaskStep
    When I select the HumanTaskStep
    Then the properties panel should show a priority dropdown with options "Low,Medium,High,Critical"
    And the properties panel should show an assignee field

  Scenario: Edit step name persists
    Given the dashboard is running
    And I have a workflow with an Action step named "MyStep"
    When I select the Action step
    And I change the step name to "RenamedStep"
    Then the step name should update on the canvas

  Scenario: Edit notes persists
    Given the dashboard is running
    And I have a workflow with an Action step
    When I select the Action step
    And I type "This does important work" in the notes field
    Then the notes should be saved
