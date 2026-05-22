Feature: Allowed roles enforcement
  Only approvers whose ID matches AllowedRoles may vote.

  Scenario: Vote from approver outside AllowedRoles is rejected
    Given a pending approval restricted to roles "sre" and "engineering-lead"
    When approver "intern" attempts to approve
    Then the vote is rejected as unauthorized

  Scenario: Vote from approver in AllowedRoles is accepted
    Given a pending approval restricted to roles "sre"
    When approver "sre" approves
    Then the approval response is Approved

  Scenario: Null AllowedRoles accepts any approver
    Given a pending approval with no role restrictions
    When approver "anyone" approves
    Then the approval response is Approved
