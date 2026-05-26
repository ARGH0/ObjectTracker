# Local IDs with operator-controlled PLC mapping

Each processing unit may keep its own Local Train IDs, while the operator controls the mapping from those local identities to PLC-facing identities, including runtime remaps. We chose this over enforcing a single global tracker ID across low-overlap, unsynchronized, distributed camera setups because the externally correct PLC mapping is the true system boundary and is easier to keep trustworthy than a universal visual identity.

## Considered Options

- Enforce one global ID for each train across all processing units.
- Allow local IDs per processing unit but keep PLC mapping fixed and non-editable at runtime.
- Allow local IDs per processing unit and let operators maintain the PLC mapping.