Feature: Approvals survive process restart
  Pending approvals persisted to IApprovalStore must be rehydrated and
  respond correctly after a simulated restart.

  Scenario: Pending approval survives restart and completes on subsequent vote
    Given an approval is requested and persisted
    When the host stops and starts again
    And approver "alice" approves via the store
    Then the rehydrated approval response is Approved

  Scenario: Past-deadline pending fires auto-action on rehydration
    Given an approval is persisted with deadline already in the past and on-timeout action AutoReject
    When the host starts
    Then the persisted approval completes with outcome TimedOut
