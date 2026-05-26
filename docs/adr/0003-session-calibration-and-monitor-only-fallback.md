# Session calibration and monitor-only fallback

The system calibrates every session from the current camera pose and lighting baseline, loads prior masks and color thresholds as starting points, and falls back to monitor-only mode when calibration confidence is too low. We chose this over fixed calibration or hard startup blocking because camera placement and lighting can change between sessions, while operators still need visibility even when the setup is not yet safe for automatic action.

## Considered Options

- Reuse fixed masks and thresholds without per-session validation.
- Block startup completely whenever calibration confidence is low.
- Calibrate per session and disable automatic action until confidence is acceptable.