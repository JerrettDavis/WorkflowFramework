@UI
Feature: Authentication
  User authentication and profile management

  @screenshot:login-page
  Scenario: Login page is accessible
    Given the dashboard is running
    When I navigate to the login page
    Then I should see the login form
    And the login form should have username and password fields

  @screenshot:register-page
  Scenario: Register page is accessible
    Given the dashboard is running
    When I navigate to the register page
    Then I should see the registration form

  Scenario: Login link is visible in navigation
    Given the dashboard is running
    When I navigate to the designer
    Then I should see a login link in the navigation

  Scenario: Dashboard works in anonymous mode
    Given the dashboard is running
    When I navigate to the designer
    Then the designer should be fully functional
    And I should be able to interact with the canvas
