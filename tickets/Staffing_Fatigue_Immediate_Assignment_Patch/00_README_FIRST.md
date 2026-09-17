# Staffing, Fatigue, and Runtime Resilience Patch

Repository: `C:\Users\artwh\BanishedInSpace`  
Unity: `6000.5.9f1`

## Purpose

Bring the newly implemented staffing system to true acceptance without restoring any pre-p2.5 production architecture. This packet is a corrective patch over the current resource-converter-based project.

Read this file and `01_LOCKED_DESIGN.md` completely before editing. Execute tickets in numeric order. A ticket may refine code written by an earlier ticket, but it may not reverse a locked decision.

## Current-work warning

The repository was dirty when this packet was authored. The staffing implementation, assets, tests, scene changes, and documentation include tracked and untracked user work. Preserve all unrelated changes. Do not reset, clean, restore, or replace files wholesale. Inspect each target before editing and make narrow changes.

## Execution order

1. `02_T00_BASELINE_AND_TEST_TRUST.md`
2. `03_T01_PERSONAL_STATUS_AND_FATIGUE.md`
3. `04_T02_IMMEDIATE_EMPLOYMENT_AND_DUTY_HISTORY.md`
4. `05_T03_STAFFING_STATE_MACHINE_AND_TRANSPORT.md`
5. `06_T04_DYNAMIC_FACILITY_PROVIDERS.md`
6. `07_T05_RUNTIME_LIFECYCLE_AUDIT.md`
7. `08_T06_CONTENT_SCENE_AND_DOCS.md`
8. `09_T07_TRUE_ACCEPTANCE.md`
9. `10_T08_SHIP_CREW_ASSIGNMENT_MODEL.md`
10. `11_T09_SHIP_CREW_HANDOVER_AND_OPERATIONS.md`
11. `12_T10_SHIP_CREW_CONTENT_AND_ACCEPTANCE.md`

Do not combine tickets into one undifferentiated rewrite. Finish the ticket's focused tests and compile gate before moving on. Later-ticket tests may be added early only when they expose behavior needed by the current ticket.

## Non-negotiable scope

Implement:

- explicit `workplace + role + shift` employment;
- immediate assignment and unassignment;
- fatigue as colonist-owned personal status;
- work fatigue and sleep recovery with extensible multipliers;
- fatigue-driven early departure;
- explicit duty records;
- deterministic post-flight reconciliation after reassignment;
- live provider discovery and enable/disable behavior;
- resilient registration when managers or components appear in any order;
- real Doctor and Nurse classes;
- trustworthy EditMode and PlayMode tests;
- current scene/prefab/data migration and documentation.

Do not implement:

- staffing UI or org chart UI;
- automatic call-in or automatic vacancy filling;
- a player toggle for automatic call-in;
- shift optimization;
- contract rerouting or multi-stop transport;
- contract cancellation except a carrier-specific pickup that becomes permanently impossible before anyone boards;
- hunger, thirst, morale, illness, or generic needs simulation;
- overtime orders or forced-work controls;
- variable fatigue thresholds from traits/equipment;
- automatic selection of unscheduled relief crew;
- a return to `FarmController`, `RecipeStaffingRule`, or recipe-owned staffing.

Those exclusions are boundaries, not questions for the executor.

## Global implementation rules

- `ResourceConverterComponent` remains the production mechanism.
- Staffing publishes facility performance; recipes and converters do not inspect colonists, roles, or shifts.
- Every staffing role requires a non-null `WorkerClassDefinition`.
- Assignment capacity counts current assignments only. There is no pending employment state.
- An invalid assignment request is atomic: report rejection and preserve the old assignment and activity.
- The current passenger trip is immutable. A reassigned passenger finishes it, unloads, then requests the next trip from the landing location.
- Disabled behaviours do no work. Re-enabled behaviours resume retained state on the next simulation tick.
- Runtime code must not contain test-only initialization branches or public setters created solely to satisfy tests.
- Tests must drive public production behavior or narrowly scoped pure methods. They must not search transport-contract history to infer who worked.

## Per-ticket completion protocol

For every ticket:

1. Inspect the listed files and their callers.
2. Add or update focused tests first when practical.
3. Implement the smallest cohesive production change.
4. Run a Unity compile and the focused test set.
5. Record any failure caused by a later ticket, but do not weaken an assertion that expresses locked behavior.
6. Update the packet checklist only after the focused gate passes.

Do not treat a green hand-built test as acceptance if the project suite is red. The final ticket requires the entire relevant suite to pass.

## Final definition of done

- Assignment changes take effect immediately in staffing and production accounting.
- A worker in flight completes the existing flight, then routes according to the new assignment.
- A worker reaches fatigue `0.90`, stops contributing that tick, and requests transport home.
- Sleeping reduces fatigue and no other activity does.
- Recovered workers do not oscillate around the exhaustion threshold.
- Explicit shift assignments remain unchanged by the simulation.
- Dynamic providers and tickable behaviours respond correctly to add/disable/re-enable/destroy operations.
- Doctor and Nurse roles can require real classes; null-class roles are invalid.
- Duty history, not contract archaeology, answers who worked and why they stopped.
- The Farm still produces through `ResourceConverterComponent` and staffing performance.
- Ship crew employment persists off duty while the responsible-pilot lease passes between explicitly assigned shifts at the shared crew-change base.
- All EditMode and PlayMode tests pass, the project compiles without errors, and a runtime smoke test has no recurring errors.
