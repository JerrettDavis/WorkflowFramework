Feature: Escalation chain
  When the primary channel times out, fall back to secondary.

  Scenario: Primary times out, secondary approves
    Given a primary channel that never responds
    And a secondary channel "email"
    And an escalation chain configured with timeout 100 ms
    When an approval is requested
    And the primary times out
    And the secondary channel receives an approval from "alice"
    Then the approval response is Approved
    And the audit trail contains an escalation record from primary
    And the audit trail contains an approval record from "alice" on channel "email"

  Scenario: Both primary and secondary time out
    Given a primary channel that never responds
    And a secondary channel that never responds
    And an escalation chain configured with timeout 50 ms for each hop
    When an approval is requested
    Then the approval outcome is TimedOut
