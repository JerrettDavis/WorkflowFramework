Feature: Approval timeout actions
  Scenarios for different on-timeout behaviors.

  Scenario Outline: Timeout triggers the configured action
    Given a pending approval with timeout 100 ms and on-timeout action <action>
    When the timeout elapses with no votes
    Then the approval outcome is <outcome>

    Examples:
      | action      | outcome   |
      | AutoReject  | TimedOut  |
      | AutoApprove | Approved  |

  Scenario: Timeout includes partial votes
    Given a pending approval requiring 3 approvers with timeout 100 ms and on-timeout action AutoReject
    When approver "alice" approves
    And the timeout elapses
    Then the approval outcome is TimedOut
    And the response includes 1 approval record
