# High-Speed Population Stress Lab — implementation report

This report accompanies `HIGH_SPEED_POPULATION_STRESS_LAB_REVISED.md`. The code-side portion is implemented in the current working tree; Unity editor execution and the final paired-speed run remain manual validation steps.

## Delivered

- `SimulationManager` now advances by a fixed simulated-second logical quantum (default one simulated second), accumulates simulation debt in double precision, carries debt forward under a per-frame catch-up cap, and preserves pause behavior. The old `gameHoursPerRealSecond` and `tickIntervalSeconds` fields remain only for scene deserialization compatibility.
- Runtime inspection surfaces expose current simulation seconds, debt, logical-step size, tick counts, active reservation counts, current facility/reservation state, and animation failure events without changing ownership of those systems.
- `StressTelemetry` provides Disabled/Summary/Detailed modes. Summary mode stores counters and a deterministic FNV-1a event digest; Detailed mode adds fixed-size event and failure rings and reports overflow instead of allocating without limit. It also keeps a bounded frame sample reservoir for p50/p95/p99 and simulation-throughput reporting.
- `PopulationStressMonitor` subscribes to existing runner/animation lifecycle events, samples invariants once per real second, and records food/off-duty query scale. It does not write files or emit per-tick logs.
- `PopulationStressHarness` runs a fixed simulated-duration campaign and exports JSON, CSV, and Markdown artifacts only at run end.
- `HighSpeedStressLabBuilder` creates a normal production-style 200-colonist scene with 200 beds, 149 workstations plus one counter, 40 food seats, 50 recreation seats, a NavMeshSurface bake, and the real managers/brains/reservation/motor/animation components. It assigns 150 workers and leaves 50 unassigned.
- Focused EditMode coverage checks stable IDs, repeatable digests, bounded ring behavior, first retained event reporting, the 1–1000 speed range, high-speed presentation factor, fixed logical-step metadata, and hard animation failure locomotion recovery.

## Known validation boundary

The repository-side C# project builds cleanly with zero errors (the existing serialized-field warnings remain). The Unity Test Runner and generated scene cannot be truthfully marked green from this session while the user’s Unity editor is open; run the manual procedure in `Assets/Dev/Stress/MANUAL_VALIDATION.md` after import/compile completes.

The first comparison should be short and detailed enough to identify whether any remaining divergence is logical, navigation-related, Animator-related, or merely presentation throughput. Do not treat a run with dropped event/failure records as a valid determinism result.
