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
