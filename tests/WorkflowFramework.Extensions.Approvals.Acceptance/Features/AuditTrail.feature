Feature: Audit trail completeness
  Every vote produces a verifiable ApprovalRecord.

  Scenario: Each vote produces a record with full audit data
    Given a pending approval requiring 2 approvers
    When approver "alice" with display name "Alice Liu" approves with comment "Looks good"
    And approver "bob" approves
    Then the response includes 2 approval records
    And the record for "alice" has display name "Alice Liu"
    And the record for "alice" has comment "Looks good"
    And every record has a timestamp within the last 5 seconds
    And every record has channel "fake"
