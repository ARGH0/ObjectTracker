# Test Documentation

Tests are written using a **Given / When / Then** structure that makes intent explicit and outcomes verifiable.

## Structure

- **Given** — The initial context or setup for the test. It describes the conditions that must be true before the action being tested runs. This includes system state, prerequisite data, and specific configurations.
- **When** — The action or event that triggers the behavior under test. This is the operation or function being exercised.
- **Then** — The expected outcome of the action. It describes what should happen as a result, including changes in state, outputs, or side effects.

## Example

```
Feature: User trades stocks

  Scenario: User requests a sell before close of trading
    Given I have 100 shares of MSFT stock
     And I have 150 shares of APPL stock
     And the time is before close of trading

    When I ask to sell 20 shares of MSFT stock

    Then I should have 80 shares of MSFT stock
     And I should have 150 shares of APPL stock
     And a sell order for 20 shares of MSFT stock should have been executed
```
