# Spacegame — corrective stack for a real high-speed test system

## Authority and required outcome

This stack supersedes conflicting assumptions and completion claims in `HIGH_SPEED_POPULATION_STRESS_LAB_REVISED.md`, `HIGH_SPEED_STRESS_IMPLEMENTATION_REPORT.md`, and the current stress manual. Retain useful existing implementation; do not restart the project.

The user wants the slider to request up to 1000x. The system must try to achieve that rate. If the machine can execute only 750 simulated seconds per real second, the ENTIRE world must advance at that achieved rate: needs, schedules, decisions, navigation, activity phases, animation-dependent gameplay, recovery, and reservation ownership. A fixed 650–700x ceiling is not the requested solution. An average measured rate is not proof of coherent progress.

Required outputs: a rewritten scene builder using real production activity recipes; executable scenarios; coherent shared slowdown; reliable diagnostics and comparisons; focused automated checks; and a step-by-step human test campaign. A document, an empty-animation scene, successful compilation alone, or a quiet Console is not completion of implementation.

Execute C00–C10 in order, consolidating related compilation/tests. Continue all independent authorized work when an engine check is blocked. Mark blocked engine evidence explicitly; never substitute a passing mock for that gate. Do not close, operate, or launch another copy of the user's Unity Editor without permission. Preserve local changes and imported assets. After two unsuccessful Unity access/licensing attempts, stop troubleshooting that access and request specific assistance. Do not commit/push as part of this stack unless requested.

## Audited starting evidence

Inspected local HEAD: `d1bf6935`, with local modifications to the stress builder, validator, manual, an unrelated animation importer meta file, and untracked generated lab assets. Reinspect status/diffs at execution time; local files are authoritative.

- `HighSpeedStressLabBuilder.ConfigureFacilityBinding` creates empty entry/active/exit arrays and supplies no loop clip. Its anchors all reference the same generated transform. The current lab cannot establish real action-animation safety.
- `ConfigureFoodService` explicitly sets `requiresStaff=false`. A work counter in the scene does not make it a staffed food test.
- `SimulationManager.Update` executes multiple logical steps per Unity frame. `PresentationSpeedFactor` returns requested speed. `PresentationTime.DeltaTime` uses real frame delta multiplied by that speed.
- `ColonistMotor` uses frame-driven native NavMeshAgent movement, scaled speed/acceleration, and frame-based placement/facing. `ColonistAnimationDriver` scales Animator speed; runner routines await frame-based engine progress. These do not establish one coherent logical timeline.
- Tick ordering uses registration order for equal-priority components. Stable IDs in diagnostics do not fix gameplay ordering.
- `PopulationStressHarness` changes speed and records exports; its scenario enum does not construct distinct scenarios. Stop does not freeze simulation, and stopping is checked in Update rather than at an exact logical endpoint.
- `PopulationStressMonitor` samples invariants in real time and flags any active critically hungry actor, including Eat. That can produce false positives and speed-dependent sampled counts.
- `StressTelemetry` reports absolute clock seconds as elapsed seconds. `StressRunComparator` does not establish manifest compatibility, complete coverage, or complete state equality; its first-event diagnostic omits tick/time comparison.
- The current free-time planner DOES compute biological headroom for unassigned colonists. Do not perpetuate the earlier claim that no upcoming work necessarily means zero recreation budget; inspect actual eligibility.

Latest inspected export: `high_speed_ProductionPopulation_20260922_161834.json`, Summary mode, 200 actors, 400 activity starts, 350 exit starts, 200 releases, 40 hunger invariant samples, 78,507 ticks, 104.198 reported real seconds, and a maximum recorded frame of 25.690 seconds.

These counters do not identify which actors or activity types completed. Remaining reservations are unclassified, not proven healthy. The hunger samples may be false positives, but Summary cannot establish that all 40 were Eat. Roughly 753 ticks per reported real second is arithmetic, not proof of sustained capacity. The previously asserted 7.1-hour debt was an inference, not a recorded measurement; frame-boundary and timing errors must be investigated. There is no evidence of end-to-end determinism from this run.

## C00 — Preserve baseline and define claims

Read modified files and original stack. Record actual behavior and prior incomplete work. Preserve prior exports. Add a correction to the implementation report rather than presenting old results as a pass.

Define independent result axes: structural validity, required activity coverage, lifecycle health, shared-time coherence, cross-speed equivalence, and achieved throughput. Each yields PASS, FAIL, or INCOMPLETE with evidence. Below-request throughput is acceptable when the world slows coherently. Zero failures with no exercised activity is INCOMPLETE.

Gate: every advertised scenario and test has an implementation location, observable evidence, and explicit acceptance criterion. An enum label or an uncalled helper is not implementation.

## C01 — Rewrite builder around real source recipes

Inspect `Assets/Bob.unity`, `Assets/bobandfriends.unity`, the colonist prefab, controller/Avatar, and the actual configured Work, Eat, Sleep, and Dance facilities. Locate source recipes by serialized asset/object identity. Do not pick a clip merely because its filename resembles an activity.

Create a lab catalog containing explicit source references and expected recipe structure. If source facilities are scene-only, extract/copy complete hierarchies into lab-owned templates with remapped local references; do not alter source scenes. Preserve anchors, targets, contacts, placement corrections, loop settings, completion modes, reverse segments, fatigue overrides, recovery configuration, meshes, and colliders. Do not leave cross-scene references. Missing usable source content is a visible authoring failure, never a fallback to an empty recipe.

Sleep must exercise the real lay-down/scooch entry, sustained rest, and reverse wake/exit. Work, Eat, and Dance must each exercise actual configured clips. A naturally empty entry/exit is allowed only when the source recipe intentionally has it; required active/loop animation coverage cannot be empty. Verify Avatar/clip/controller compatibility and ActionA/ActionB/locomotion paths using the actual controller.

Generate a visible, lit lab with cameras and selectable actors, a 4-person visual fixture, 2-person contention fixture, and configurable 50/200/500 populations. Keep the canonical 200-person resource counts: 200 beds, 149 workstations plus one counter, 40 food slots, 50 recreation slots, 150 assigned and 50 unassigned. Use deterministic names/IDs, seeded state, and versioned configs.

Build into owned temporary output, validate, then replace only lab-owned outputs with recoverable handling of previous generated content. Preserve open/dirty user scenes. Avoid generating another role asset on every rebuild. Persist baked NavMesh data as an asset and prove it survives scene reload.

Gate: catalog lists each assigned clip GUID/local ID, direction/speed, duration, anchor/target, and facility mapping. Builder rejects missing required recipes. Reloaded fixtures retain all references. Provide before/after structural checks for missing clip, missing anchor, and invalid Avatar/controller.

## C02 — Prove navigation and startup eligibility

Lay out facilities using actual geometry/anchor extents and agent clearance; do not assume the placeholder 1.3m boxes match furniture. Bake static lab geometry with the correct agent type, excluding dynamic actors. Verify every spawn and approach/exit for matching agent type/mask, acceptable projection displacement/height, complete paths, and connected islands. A nearby polygon on top of furniture or across a wall is insufficient. Test reachable routes through the busiest zones and create a separate intentional choke scenario.

Apply starting hour, needs, assignments, manager registrations, and service state before any tick. Start paused/ready by default; Start immediately releases a fully prepared scenario. Support auto-start only after the same validation barrier. Each scenario specifies expected first-tick intentions; eligible actors must not wait hours for thresholds. Show why any actor is intentionally waiting or ineligible.

Gate: seeded recreations have identical initial fingerprints and IDs. Work/food/sleep/recreation scenarios independently demonstrate immediate eligible decisions. Bake validation catches an intentionally out-of-bounds anchor. No production navigation tolerance is widened to hide bad layout.

## C03 — Establish one committed simulation timeline

This is a runtime correction, not something the scene builder can solve alone. Audit all consumers of Unity delta time, PresentationTime, coroutines, Animator state/callbacks, navigation/path readiness, root motion, placement, facing, and logical ticks. Document their ownership and update order.

Implement a shared advancement contract: requested rate creates a target pace; a bounded scheduler executes complete simulation quanta as resources allow. Only committed quanta advance the authoritative world. All gameplay participants consume the same interval and boundary ordering. Presentation may interpolate or skip drawing intermediate poses, but cannot move gameplay ownership or finish a meal early. No consumer may apply requested-speed wall time in addition to committed time.

The system must attempt 1000x and adapt to actual capacity without a fixed effective-speed ceiling. Bound real CPU work per frame to preserve responsiveness. An overload target is a pace request, not an unlimited obligation to accumulate hours of catch-up. Bound admission of future work; export unadmitted requested time separately from admitted/unexecuted work. Never skip an already admitted logical interval, enlarge quanta under load, or silently advance the clock. Avoid a later catch-up burst that violates the shared pacing contract. Requested, admitted, committed, pending, and pace shortfall must reconcile.

Do not equate setting Animator.speed and agent.speed to the same average ratio with synchronized progress. Specify when each consumes a quantum and when completion becomes visible to brains. Choose a fixed quantum from low-speed fidelity evidence and use it at every requested speed. Preserve the established stats/brain semantics or explicitly test any boundary correction. Make tie ordering independent of Unity creation/registration order, using stable simulation identities.

Gate: with a controlled CPU restriction and requested 1000x, all authoritative consumers advance through identical committed intervals, even when throughput falls to 750x or lower. Removing the restriction restores available throughput. Pause commits zero time across every phase; resume does not count paused wall time as demand. A controlled hitch produces shared slowdown and bounded pending work, not selective progress.

## C04 — Resolve native navigation and real animation coupling

Implement and test this on the two/four-person fixtures before scaling. Native NavMeshAgent avoidance/path completion and Animator transitions are frame-driven; there is no assumption here that Unity supports arbitrary manual deterministic stepping of them.

For animation, give logical entry/active/dwell/exit durations and completion an explicit timeline based on the real authored recipe, including reverse playback, blends, loops, contacts, and placement. Preserve low-speed behavior. Advance across multiple boundaries with elapsed remainder exactly once. Evaluate real clips for presentation at the committed phase/time; avoid double evaluation/root motion. A missing visual frame must not shorten or prolong gameplay. Engine watchdogs use unscaled active wall time, suspend during pause, and report presentation faults separately; hard failure restores locomotion and follows a tested ownership policy.

For navigation, prove the selected implementation supports the same shared advancement and collision/avoidance semantics at all speeds. Test route readiness, acceleration, turns, arrival, contact with other agents, and dynamic blockage. If native stepping cannot meet equivalence, expose the limitation with a reproducible probe and implement the smallest explicit production movement correction needed. Do not quietly replace avoidance with a path-length timer, teleport actors, or disable collision only at high speed. Any changed movement policy must also run at 1x/10x and pass route/obstacle/avoidance regression evidence. Rendering can lag within a declared bound; positions used by food selection and reservations cannot use stale presentation transforms.

Gate: same-build single-trip and two-agent contention runs agree on authoritative arrival and ownership across requested speeds and frame caps. Real sleep/reverse-wake, meal, dance, and work recipes execute and release/reacquire correctly. Missing Animator state, impossible route, pause mid-transition, and cancel during placement recover without leaking ownership. If native avoidance remains nondeterministic, mark equivalence FAIL/INCOMPLETE; shared slowdown alone does not prove determinism.

## C05 — Build executable scenarios and fresh reset

Scenario selection must actually instantiate/apply a config, validate its eligibility, and display required coverage. Preserve real production brains/managers/services in end-to-end runs. Implement:

| Scenario | Setup and required observed outcome |
|---|---|
| Visual lifecycle | Four actors, real recipes, scripted obligations/needs; observe entry, loop/activity, exit, locomotion and reacquisition for every activity type. |
| Work stampede | Healthy assigned cohort starts at shift boundary; all obtain valid work goals and activate; shift end exits and releases. |
| Food contention | Critically hungry cohort versus scarce seats; all eventually eat after slots free, with per-actor waits and repeat service recorded. |
| Staffed food | Actual assigned chef reaches active Work; service enables through real staffing checks. Disable/re-enable service/chef at scheduled ticks. Include an explicitly unstaffed control config. |
| Critical hunger denial | Interrupt both Sleep and Work at critical threshold while food is unavailable; awake idle food retries, no Work/Sleep/OffDuty restart; restore food and prove recovery. |
| Sleep/wake | Off-shift sleepy cohort, low hunger, dedicated beds; recovery to wake threshold, real reverse wake/exit, release and next action. |
| Recreation | Off-shift high drive, low hunger/fatigue, demonstrably sufficient biological/shift budget; choose Dance, complete its duration, recover need, enforce cooldown, later reacquire. |
| Mixed colony | Seeded employed/unassigned population over 24h/72h; observe meals, shifts, sleep, recreation, thresholds and midnight. |
| Navigation choke | Real traffic, scarce passage, measurable route progress, no off-mesh or permanent deadlock. Label any brain-disabled diagnostic separately. |
| Pause/failure/overload | Pauses in approach/entry/active/exit, CPU restriction, hitches, cancellation, disabled/destroyed owned actor/facility, failure followed by successful reacquisition. |

Use tick-indexed inputs. For an unavailable-food case, measure repeated failed searches separately from accepted activity requests. Do not tune needs to mask starvation, and do not bypass production eligibility just to color a scenario green.

Reset by recreating owned state, not just resetting counters. Include static registrations/ordinals, subscriptions, reservations/generations, cooldowns, RNG, coroutines, paths, animation overrides, and singletons. Prove fresh runs with domain reload enabled and disabled. Restore any modified frame/vsync/profiler/log settings.

Gate: each scenario has explicit per-actor or cohort liveness bounds in simulated time and fails when expected activity never happens. A manual abort cannot pass it.

## C06 — Correct observation and expose real failures

Exclude active Eat from the critical-hunger normal-activity invariant. Check transitions at committed logical boundaries; account for a legitimate exit already requested at the defined boundary. Add evidence including actor, hunger, brain state, activity, phase, stop request, facility and reservation generation. Do not blanket-exempt ongoing Work or Sleep.

Instrument actual authoritative state changes. Keep lightweight counters per activity and cohort: eligible/attempted/denied/reserved/navigating/entry/active/exit/released/completed/failed, unique actors served, wait distribution/max, no-progress durations, recipe segment coverage, reverse wake coverage, need minima/maxima, starvation periods, and current ownership. Detect duplicate release, invalid token generation, orphaned facility tokens and runner-held tokens, and permanent phase stalls. Inspect both sides of ownership.

Classify reservations at endpoint as legitimate active lifecycle, pending exit, or leaked; export that state before cleanup. Then perform explicit owned cleanup and verify zero remaining reservations/subscriptions. End-of-horizon need not mean everyone is idle, and zero remaining reservations is not a valid requirement before cleanup.

Keep timing/performance sampling separate from semantic events and their digest. Wall-time sampling frequency must not alter logical pass/fail or hashed event counts. Retain a bounded first-failure capture in Summary mode as well as aggregate counts. A counter named InvalidNavigationState must have an actual producer. Detect finite/valid numeric values and record invalid data as failure.

Gate: injected illegal Work under critical hunger fails; critically hungry Eat passes; injected stale ownership/stall is detected; omitted activity coverage fails even with zero exceptions. Event counts remain total counts in Summary, separate from retained record counts.

## C07 — Trustworthy run boundaries, measurement and comparison

Start at a defined ready boundary with absolute start clock, start tick, monotonic real timestamp, and initial fingerprint. End inside the shared scheduler at the exact target tick, freeze, capture final metrics, then export. Avoid Update-order snapshot lag and including the pre-start frame delta. Record end reason: target reached, manual abort, invariant failure, engine exception, blocked progress, observation overflow or export failure.

Elapsed simulation = end committed time minus start committed time. Elapsed real time uses matching monotonic boundaries; report intentional paused time separately. Export requested speed schedule, achieved rate over whole run/windows, admitted/committed/pending time, pace shortfall, CPU budget hits, scheduler/component cost, GC allocations/collections, frame distribution and hitch context. Use achieved rate as an observation, never as an independent gameplay clock.

Manifest includes schema/build/dirty fingerprint, config/seed, exact initial state, resource capacities, serialized recipes and imported asset fingerprints, NavMesh/settings, Unity/packages, ordering/quantum, simulation/presentation modes, input script, observation settings, and intended endpoint. Speed/frame cap are intentional comparison axes, not blanket incompatibilities.

Compare compatible runs using ordered semantic events AND stable ordered state checkpoints: needs, authoritative positions/routes, brain targets/states, lifecycle phases/remaining durations, reservations/generations, staffing, cooldowns and pending inputs. Include tick/phase in event comparison. Keep performance differences outside semantic equality. A digest is a fast check, not sufficient diagnostic evidence. Report first divergent tick/field/event with bounded preceding context; retain per-block hashes to locate a detailed rerun if a full trace is not stored.

Missing coverage, incompatible inputs, abort, trace gaps, and unvalidated state fields yield INCOMPLETE, never PASS. Never compare the old broken fixture against the rewritten fixture as an equivalence pair.

Export automatically to `<project>/StressResults/<unique-run-id>/` in Editor, print absolute paths, provide Open Results and Compare actions, and preserve every prior run. Export final state, manifest, named metrics JSON, readable summary and bounded event/failure evidence. Stop/pause simulation before serialization so disk time does not advance the world.

Gate: tests catch the 08:00 offset error, mismatched boundaries, missing tail, altered actor/target/tick, changed manifest, abort and overflow. Repeatable healthy slower-than-request runs can pass coherence while truthfully reporting lower throughput.

## C08 — Keep instrumentation affordable

Cache references/IDs and avoid per-tick scene searches, formatted strings, stack traces, sorting the population, full-world JSON snapshots, or synchronous file writes. Use compact semantic records, bounded buffers, scheduled checkpoint frequency, and background/chunked output only with safe ownership and an explicit overflow policy. Capture targeted detail around first failure. Query counters alone must not emit millions of records.

Measure diagnostics-off, counters/summary, and detailed overhead on the same fixed workload; runtime measurement must work with semantic recording disabled. Report CPU/GC/memory and throughput deltas. Target summary overhead at or below 5% in sufficiently long repeated runs; if measurement noise prevents a verdict, report INCOMPLETE and rerun. Detailed mode may cost more but must be bounded and disclosed. Never reduce world fidelity to improve a telemetry benchmark.

Gate: the existing 5.9-million food candidate pattern is explained using per-actor denial/retry reasons and measured cost. Do not assume it is the bottleneck or add queues as an unrelated redesign.

## C09 — Automated acceptance and integration

Compile runtime, Editor and test assemblies with the installed Unity version, then run focused behavioral tests once the related changes are complete. Add regressions for real recipe presence/reference remapping, persisted bake/reload, immediate eligibility, fresh reset, the shared-clock accounting identities, pause/hitch/overload, stable ordering, endpoint capture, invariant false positives, coverage failure, and comparator rejection cases.

Use native PlayMode tests for real Animator and NavMesh interaction. Include two-agent competition, reverse wake, action-state failure recovery, cancellation with a held reservation, and another successful request. Pure scheduler tests do not replace native evidence. Do not use tests that merely assert constants or mirror private implementation.

Maintain a verification matrix linking every gate to a test result/artifact or explicit pending human operation. Separate checks that ran from those merely written. Do not declare readiness with unresolved compile errors or missing content. Any performance/coherence limitation must be visible before the user launches a long campaign.

## C10 — Human campaign and handoff

Implement a lab window with these concrete controls, then publish matching actual paths/names in `Assets/Dev/Stress/MANUAL_VALIDATION.md`: Build/Validate, scenario/population/speed/duration selection, Start, Pause/Resume, Stop and Export, Fresh Reset, Run Pair/Matrix, Compare, Open Results. Show Requested/Achieved speed, committed elapsed time, pending work, activity coverage and first failure. Button labels below are required deliverables, not claims about today's UI.

The user should follow these steps after C00–C09 are implemented and compilation is green:

1. Save the working scene and leave Play Mode. Refresh assets. Open the lab window, choose **Build/Validate**. It must report real recipe coverage, persisted NavMesh validity, and prepared scenarios. Stop if any required clip/anchor/source is missing; do not hand-wire components.
2. Choose **Visual lifecycle**, population 4, requested 1x, and **Start**. Inspect one complete Eat, Work, Dance, Sleep/reverse-wake cycle and return to locomotion. Prepared short diagnostic thresholds must allow seeing transitions without waiting a day; use 10x for long active intervals and 1x for entry/exit, recording speed inputs. Verify clip/segment counters, not appearance alone.
3. Choose **Fresh Reset**, the 2-person contention scenario, a 600-simulated-second horizon and **Run Pair/Matrix** for 10x twice, then 1000x twice, at the same initial state. If a recipe requires a longer horizon, the UI must supply it. Compare automatically at exact endpoints. A 600-second 10x run takes about 60 real seconds; do not use its subsecond high-speed counterpart to estimate sustained performance.
4. Run **Pause/failure/overload** on four actors at requested 1000x. Keep the request at 1000x while applying/removing controlled CPU restriction. Confirm shared committed progress and bounded pending work. Pause in each phase longer than the watchdog window and resume. Every injected failure must be detected, ownership cleaned up, and a subsequent action succeed.
5. Run the 50-person work, staffed/unavailable food, sleep, recreation, and choke scenarios. Each must meet its own coverage and recovery gates. Compare 10x against 1000x from fresh identical state; do not treat a mixed scene as proof all activities were exercised.
6. Run canonical 200-person fixtures at 10x/100x/250x/500x/1000x and multiple frame caps through identical endpoints. The matrix automates resets and exports, stops on first actionable failure, and points to the earliest divergence. Below-request achieved speed is acceptable if coherence/health pass; equivalence has its own verdict.
7. Run the seeded mixed colony for an exact 24 hours, then 72 hours only after shorter gates pass. Automate baselines and repetitions; 24 hours at 10x takes 2.4 real hours and 72 hours takes 7.2 real hours. Never ask the user to sit watching a baseline. Coverage must prove shift changes, repeated meals, sleep/wake, recreation/cooldown and fair-enough food access under the declared supply scenario.
8. Run observation-cost comparisons and the optional 500-person scaling case. Report shared slowdown, CPU/GC and traffic bottlenecks rather than automatically interpreting a lower achieved rate as failure.
9. Use **Open Results**. The final report links each run/pair and gives separate structural, coverage, health, coherence, equivalence and throughput outcomes, plus pre/post-cleanup state. Pending/aborted/missing tests stay INCOMPLETE.

## Completion contract

Implementation is complete only when the real builder, real scenarios, shared-time runtime contract, reliable reports, comparison controls, automated checks and exact manual procedures exist and are verified as far as the available engine access permits. A blocked manual/native gate is disclosed explicitly. No claim that 1000x is safe or deterministic is allowed until its relevant evidence passes. No fixed effective ceiling is introduced as a substitute for trying the requested speed and slowing the entire simulation together.
