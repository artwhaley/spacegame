# OffDuty / Biological Planning / Structured Log Report

## Implemented

- `SimulationLogManager`, `SimulationLogEntry`, `SimulationLogField`, and structured subjects provide one bounded in-memory history with JSONL output, Console rendering, semantic event keys, structured fields, subject/target snapshots, and query filters.
- `ReadinessHistory` is now a compatibility adapter into the canonical structured log.
- `ColonistActivityRunner` exposes generic `ActivityLifecycleEvent` telemetry without depending on the game-side logger.
- Hunger thresholds now distinguish Hungry 60, Critical Hunger 90, and Starving 100. Fatigue distinguishes proactive Rest Preferred 60 and hard Sleepy 70.
- `ColonistBrain` allows ordinary Eat inside the 30-minute Work preparation window, prevents newly-started Sleep/OffDuty there, lets Critical Hunger interrupt Work or Sleep, and supports `OffDutySeeking` / `OffDutyActive` lifecycles.
- `OffDutyComponent` / `OffDutyManager` provide registration-safe nearest fitting discovery without reservation.
- `ColonistFreeTimePlanner` calculates biological headroom and protected Sleep budget without commanding activities; travel time is intentionally zero for this slice.
- Optional OffDuty staffing requires assigned workers to be inside their current shift and genuinely active at the matching workplace activity for the full planned duration.
- Bob's existing Recreation facility is authored with `OffDutyComponent`, `play`, one game-hour planned duration, and no staffing requirement. The Bob scene contains `OffDutyManager` and `SimulationLogManager`.
- Bob's live inspector now exposes critical hunger, proactive rest, next Work, time until Work, protected Sleep, discretionary budget, OffDuty target/duration, and last decision/reason.

## Validation boundary

Source searches confirm the Unity 6 obsolete `GetInstanceID` and `FindObjectsSortMode` calls are absent from the relevant implementation. `git diff --check` reports only Unity YAML empty-value trailing-space warnings.

The generated Unity project files were stale during this unattended pass, and the available .NET fallback build could not pass the host `Microsoft SDKs` ACL. Unity Play Mode and EditMode compilation therefore remain a human/editor verification gate. Do not treat this report as a claim that Unity compilation or Play Mode has passed.

## Human acceptance sequence

1. Refresh Assets in Unity and wait for script compilation to finish. Run the EditMode suite and resolve any compiler/import issue before Play Mode.
2. In Bob's inspector, set Hunger to 60–80, Fatigue below 60, and place the next Work shift roughly 20 minutes away. Enter Play Mode. Bob should discover and begin Eat instead of idling; when the shift becomes current, he should physically exit Eat and then request Work.
3. Set Fatigue to 80 and Hunger above 90 with no immediate Work. Eat should win over Sleep. Lower Hunger below 90 while keeping it above 60; Sleep should win.
4. Make Bob Critically Hungry at shift start. He should Eat before Work. While genuinely Working, raise Hunger above 90; he should physically leave Work, release it, and then Eat. The structured history should contain `work.left_for_critical_need`.
5. Set Hunger below 60, Fatigue below 60, and Work comfortably distant. Bob should log an OffDuty decision, reserve `Recreation/play`, navigate, become active, remain active for the planned one game-hour, exit, release, and reconsider.
6. Set Fatigue between 60 and 69, with Hunger below 60 and no immediate higher-priority need. Bob should choose Sleep instead of OffDuty. Set Work close enough that the planner rejects a one-hour activity; the log should show `insufficient_rest_budget`, `rest_preferred_threshold`, or another explicit budget reason rather than a silent idle.
7. Inspect `SimulationLogManager` during the run. Verify structured entries explain decision, reason, target, reservation, active start, exit, release, and interruption without parsing the rendered Console sentence. Querying by Bob or Recreation should return the same visit events.
