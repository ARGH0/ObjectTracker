# Operator resolves identity ambiguity

Object Tracker treats wrong train identity as the primary safety failure, so when identity becomes ambiguous the system must exclude that train from automatic action, raise a blocking alert, and require operator resolution rather than guessing. We chose this over best-effort autonomous reassociation because the project explicitly tolerates human intervention but not silent ID swaps.

## Considered Options

- Continue tracking and silently choose the most likely identity.
- Freeze updates until confidence recovers without operator involvement.
- Require operator resolution before the train can re-enter automatic action.