# High-Speed Population Stress Lab — manual validation

The implementation is intentionally split into two phases: the editor builder creates a normal production-component scene, and the runtime harness only controls speed, observation, and export. The final Unity run is still required because NavMesh baking, Animator state entry, and real frame scheduling are engine-level behavior.

## Build the scene

1. Let Unity finish importing and compiling.
2. Open **Tools → Spacegame → Build High-Speed Population Stress Lab**.
3. Open `Assets/Dev/Stress/Generated/HighSpeedStressLab.unity`.
4. Confirm the scene contains:
   - 200 `StressColonist_*` actors;
   - 200 beds;
   - 149 staffed workstations plus one staffed counter;
   - 40 food seats;
   - 50 recreation seats;
   - a `NavMeshSurface` with baked data;
   - `SimulationManager`, `StressTelemetry`, `PopulationStressMonitor`, and `PopulationStressHarness` under `Systems`.
5. Run **Tools → Spacegame → Validate High-Speed Population Stress Lab** and inspect `Assets/Dev/Stress/Generated/HighSpeedStressLabValidation.md`.

The generated scene is authored at simulation hour 08:00 and starts paused for inspection. Its deterministic startup cohorts are: colonists 000-119 Work, 120-169 Eat (40 seats deliberately contend for 50 actors), 170-189 Sleep, and 190-199 Dance. Every facility binding uses the same production clips audited from `bobandfriends.unity`, including reverse playback of the sleep exit clip.

## Canonical 1000x run

1. Run **Tools → Spacegame → Build High-Speed Population Stress Lab**. This replaces the generated scene and stable role asset, but does not modify production scenes or prefabs.
2. Run **Tools → Spacegame → Validate High-Speed Population Stress Lab**. Do not continue unless the generated report has zero errors. In particular, every activity must have a loop clip and every approach/exit anchor must sample onto the baked NavMesh.
3. Open `Assets/Dev/Stress/Generated/HighSpeedStressLab.unity` and enter Play Mode. The scene does not auto-start.
4. Inspect several representatives from each cohort and confirm the Animator/controller and NavMeshAgent are present.
5. On `Systems`, invoke **PopulationStressHarness → Begin Stress Run** from the component context menu. This unpauses the shared simulation clock and requests 1000x.
6. Watch at least one actor from each cohort enter its production loop. Work and Eat must sit/type, Sleep must lie down and later reverse the same clip to stand, and Dance must play the production hip-hop loop.
7. Allow the one-day run to finish. The harness pauses the simulation before exporting, so no post-run state can contaminate the artifact.
8. Inspect the exported manifest/summary/events. Required activity coverage for Work, Eat, Sleep, and Dance must be nonzero; animation/activity failures, ownership mismatches, orphaned reservations, and missing coverage must be zero.
9. Record requested simulation seconds, committed simulation seconds, pace shortfall, maximum debt, and presentation factor. If the machine sustains less than 1000x, committed logical time and presentation must report the same achieved pace; requested pace remains 1000x and the difference appears as pace shortfall.

## Scenario selection

The `PopulationStressHarness` scenario is now applied at the beginning of the run while the simulation is paused. Select the scenario in the Inspector before pressing **Begin Stress Run**; reload the scene between runs so each run starts from a clean population.

- `ProductionPopulation`: synchronized control. It preserves the authored startup cohorts and 08:00-16:00 work shifts.
- `StaggeredPopulation`: deterministic real-world-style profile. Every colonist receives a different bounded hunger/fatigue/leisure phase, a small distributed critical-hunger cohort, and assigned workers receive deterministic staggered eight-hour shifts.
- `FoodContention`: all 200 colonists begin critically hungry, leaving the 40 food seats as the intentional bottleneck.
- `RecreationContention`: all 200 colonists begin with a stimulation need; assigned workers still retain their work obligations, so this measures whether recreation enters the rotation after obligations rather than pretending everyone is immediately off duty.
- `PairedSmallFixture` and `FailureRecovery`: labels retained for future dedicated fixture builders; the current 200-colonist scene does not implement those scenarios and they should not be treated as executed tests.

For the next comparison, run `ProductionPopulation` once as the synchronized control, then rebuild/reload and run `StaggeredPopulation` at the same requested speed and duration. Compare committed time, shortfall, frame quantiles, lifecycle coverage, waits, and failure records—not just the final throughput number.

If the scene builder reports a missing prefab or the NavMesh surface has no data, stop there and fix that authoring issue before interpreting stress results.

## Paired speed comparison

Use a short run first. Select `Systems`, set `PopulationStressHarness`’s `Run Duration Simulation Seconds` to `3600` (one simulated hour), and set `StressTelemetry` to **Summary**. Run the same freshly reloaded scene twice:

1. Set the harness speed to `10`, use the component context menu **Begin Stress Run**, wait for the export message, then use **End Stress Run and Export** if it does not stop automatically.
2. Reload the scene so all runtime state and reservation generations reset.
3. Set the harness speed to `1000`, repeat the run, and let it export.
4. Compare the two JSON artifacts’ `Snapshot.Digest`, counters, simulation seconds, and simulation ticks. Matching digest/counters is the deterministic target. A mismatch is useful: switch to **Detailed**, repeat with a shorter duration, and compare the retained event CSVs. The comparator’s “first retained event difference” identifies where divergence begins.

The export files are written to Unity’s `Application.persistentDataPath` when `Export Directory` is blank. Unity prints the exact JSON path in the Console. The JSON, `_events.csv`, and Markdown report share the same run stem.

## Detailed failure/recovery pass

Set `StressTelemetry` to **Detailed**, use a 600–3600 simulated-second run, and inspect:

- `Snapshot.DroppedEvents` and `Snapshot.DroppedFailures` must be zero for a bounded comparison;
- `AnimationFailures` and `ActivityFailures` must be zero for a clean run;
- `InvariantViolations`, `ActiveWithoutReservation`, `OrphanedReservations`, and `CriticalHungerNormalActivity` must be zero;
- the exported event CSV must show each reservation acquired/released exactly once around an activity lifecycle.

If an animation watchdog fires, verify the affected Animator returns to `Locomotion` and that the actor later acquires another activity. A single failure must not strand the runner or leave a reservation held.

## Pause check

Start a short run, pause `SimulationManager`, wait in real time, then resume. During the pause, `CurrentSimulationSeconds` and `CurrentTick` must remain unchanged; after resume, carried simulation debt must continue without a time jump. The stress monitor may continue counting real frames while paused, which is intentional and separates wall-clock overhead from simulation time.

## Interpreting a failure

- A digest mismatch with an early event mismatch is a logical-order or navigation/animation lifecycle divergence.
- Equal event digest but different frame counts is a presentation/throughput difference, not a simulation-log difference.
- Nonzero dropped rings invalidate only the detailed trace; rerun with a shorter window or larger configured rings.
- High food/off-duty candidate counts are expected to scale with population. Use the exported counters to distinguish a true query explosion from a normal 200-agent workload.
- The Markdown report includes bounded frame p50/p95/p99 and simulated-seconds-per-real-second throughput. Use those values to separate a matching gameplay trace from a machine that is simply too slow to sustain the requested speed.
