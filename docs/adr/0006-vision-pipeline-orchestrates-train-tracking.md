# Vision Pipeline orchestrates Train Tracking

The operator starts and stops one Vision Pipeline, but Train Tracking remains a distinct internal module rather than being collapsed into per-Camera Source visual observation lanes. The Vision Pipeline coordinates Camera Source lanes that produce Moving Object Observations and Train Observations, passes those observations into Train Tracking for Processing Unit-wide Local Train ID continuity and Train State, and emits per-Camera Source PipelineSnapshot output for the UI. We chose this over separate Train Tracking runtime controls or lane-local train identity because operators need one runtime flow while the code needs locality between visual evidence and train identity continuity.

## Considered Options

- Put all Train Tracking logic inside each Camera Source lane.
- Run Train Tracking as a separately controlled runtime.
- Let the Vision Pipeline orchestrate visual observation lanes and an internal Train Tracking module.
