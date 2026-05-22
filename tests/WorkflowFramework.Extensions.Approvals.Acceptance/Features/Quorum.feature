Feature: N-of-M quorum approval
  As a workflow designer
  I want to require multiple distinct approvers
  So that high-stakes decisions get appropriate oversight

  Background:
    Given an approvals service configured with quorum support

  Scenario: 2-of-3 succeeds on second approval
    Given a pending approval requiring 2 approvers out of 3 candidates
    When approver "alice" approves
    And approver "bob" approves
    Then the approval response is Approved
    And the response includes 2 approval records

  Scenario: 2-of-3 with one rejection still succeeds when remaining approvers approve
    Given a pending approval requiring 2 approvers out of 3 candidates
    When approver "alice" rejects
    And approver "bob" approves
    And approver "charlie" approves
    Then the approval response is Approved

  Scenario: 2-of-2 short-circuits on first rejection
    Given a pending approval requiring 2 approvers out of 2 candidates
    When approver "alice" rejects
    Then the approval response is Rejected

  Scenario: 1-of-3 completes on first approval
    Given a pending approval requiring 1 approver out of 3 candidates
    When approver "alice" approves
    Then the approval response is Approved
    And exactly 1 approval record is present
