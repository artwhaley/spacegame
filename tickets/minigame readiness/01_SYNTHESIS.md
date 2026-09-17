# Consolidated Readiness Synthesis

Baseline: `15fb66ba52ea210d57f593acc4ce4d70ce618a31`

Overall verdict: **ready with targeted repairs, not a rewrite**.

The existing architecture is directionally correct and should be preserved: local inventory authority, explicit employment, staffing published as facility performance rather than embedded in recipes, a separate temporary pilot lease, transport arbitration before materialization, discrete-quantity normalization, and the simulation tick registry are all worth keeping.

The remediation is about making live component experimentation, presentation, lifecycle recovery, and observability truthful enough to support the next playable slice.

---

## 1. **Run the real Unity suites before treating the baseline as known-good.**

> The repository proves source compilation, but EditMode/PlayMode execution was still pending at the reviewed baseline. At least two pilot-duty assertions appear likely to encode stale semantics. Run the actual Test Runner first. If a test is stale and the runtime matches the documented intended behavior, fix the test rather than distorting the runtime to satisfy it.

## 2. **Disabling a facility's staffing component must not turn that facility into an automated full-output facility.**

> A disabled `StaffingComponent` currently disappears from provider evaluation, while an empty provider set defaults to operational `1.0`. Workers may also continue to be treated as working. Distinguish "no staffing provider exists" from "this staffing provider exists but is disabled." Disabled staffing should block the staffed facility and release/stop its workers appropriately.

## 3. **Logical containment must not be Unity transform ownership.**

> `ColonistAgent.MoveToLocation()` reparents the colonist under ships/facilities. This means disabling or destroying a ship can disable/destroy its occupants, affecting population/staffing registries. Keep people under a stable hierarchy and represent location/containment explicitly.

## 4. **Arrival must mean physically arrived, not "the simulation decided the destination early."**

> For the next milestone, colonists must visibly walk to beds, seats, and workplaces, and ships must visibly dock. `currentLocation` should remain the authoritative **arrived** location. Add explicit transit origin/destination/state rather than setting the destination immediately and letting visuals catch up. Ships likewise need a real movement/docking phase rather than a single `IsTraveling` boolean.

## 5. **Missing or disabled ship movement must block travel, not teleport.**

> Transport and extraction currently treat a missing movement component as successful arrival in some paths. Under the project's component experimentation model, removing a mover must produce an inspectable blocked state. If instant movement is useful for tests, make it an explicit implementation.

## 6. **Vehicle/passenger teardown needs deterministic recovery.**

> Destroying a vehicle can strand contracts, freight reservations, movement leases, and passengers. Destroyed/null passengers can also poison manifests. Define authority and teardown rules: the contract is the transport obligation; carrier occupancy is physical membership; colonist transit/location must reconcile to those. Teardown must cancel/reopen/unwind explicitly rather than silently deadlock.

## 7. **Disabled colonists must still consume their persistent employment slot.**

> Shift capacity currently risks counting only active/enabled workers. Disabling one worker can create an apparently free slot and re-enabling them can produce more persistent assignments than the authored cap. Capacity is about employment, not current GameObject enablement.

## 8. **Duty phase needs one writer and one meaning per state.**

> Duty state is currently written from multiple components/predicates in the same simulation cycle. It can also lie: a pilot executing committed work may report `AcceptingNewWork`, and an off-shift passenger may report `ReturningHome` while traveling toward a workplace. Compute phase once after reconciliation. Add a `Blocked` state if needed. `CompletingCommittedWork` should mean an already accepted ship operation must finish before release.

## 9. **Reconcile legality before applying work/fatigue consequences.**

> At a shift boundary the current order can apply one extra tick of work/fatigue before discovering the worker is no longer legally on shift. Reconcile first, then apply work/fatigue to the resulting state. Do not weaken the explicit rule that a pilot finishing accepted work or flying home remains on duty and accumulates fatigue.

## 10. **Vehicle availability and the explanation for unavailability must be the same rule.**

> The code has overlapping availability/readiness predicates that can disagree, producing diagnostics equivalent to "none available [Shuttle: available]." Replace drift-prone parallel logic with one query that returns both the boolean result and blocker reason.

## 11. **UI should use narrow validated commands and truthful queries, not raw runtime fields.**

> Staffing already has the right idea with `Assign() -> AssignmentResult`, but its dry-run/eligibility surface is incomplete. Expose exact validation results for assignment candidates and add small validated methods for actual player commands as those screens land. Do not create a universal command bus.

## 12. **Use explicit runtime registries for inspection and simulation hot paths; stable persistent IDs can wait.**

> Per-tick `FindObjectsByType` scans contradict the intended registration model, allocate, and cannot see inactive GameObjects. Register ships/facilities/inspectables and expose read-only collections. Persistent stable scene IDs are useful later for save/load/external references but are not required to build the first in-session management UI.

## 13. **Disable/pause/cancel semantics must be explicit and consistent.**

> "Disable" should normally mean pause for experimental components, but a paused operation must not continue claiming progress or silently accruing impossible work. Disabled extraction, paused freight, disabled stock policies, and disabled staffing need explicit suspended/blocker behavior. Explicit cancellation is a separate command that unwinds ownership/reservations.

## 14. **A multi-day run must be explainable after Play Mode ends.**

> The rolling log and small duty histories are useful live but insufficient for post-mortem debugging. Add a lightweight transition-only durable artifact or dump: assignments, duty start/release/end, contract lifecycle, docking/location transitions, blocker changes, shortages, and relevant IDs. No per-tick logging and no telemetry platform.

## 15. **Fix scene/content contradictions so the slice can demonstrate its own loop.**

> The reviewed scene has a logical/visual mismatch for the mining ship and incomplete selection/collider coverage. The authored food economy also appears unable to sustain the current population long enough to demonstrate the intended loop. Fix these as content/scene work, not architecture changes.

## 16. **Inventory remains the only inventory authority; racks are views.**

> Visible boxes/racks must project `InventoryComponent` state and never own a second count. An inventory-changed event is a useful convenience once visual consumers exist, but the lack of one is not itself a foundation blocker. Add the smallest presentation seam that keeps racks read-only.

## 17. **Apply cheap latent hardening while touching nearby seams.**

> Recommended surgical hardening: fixed authored `crewChangeBase`; serialize converter batch state; refuse recipe switches during active batches; startup-validate scene-authored employment; make contract/demand counters continuity-safe; encapsulate runtime-authoritative fields that UI would otherwise poke directly.

## 18. **Do not repair the architecture that is already working.**

> Do not replace the MonoBehaviour network, recipe/staffing separation, employment model, pilot lease, arbitration model, quantity rules, extraction separation, no-call-in staffing policy, or finish-current-flight behavior. Do not add full save/load, predictive routing, a universal work-order model, or a new scheduler.

---

# End-state effects this packet must produce

When the packet is complete:

- disabling/re-enabling a component produces a coherent pause/block/recovery behavior rather than silently changing simulation meaning;
- workers and pilots have truthful duty/activity/location states suitable for UI;
- people can physically move without becoming children/lifetime dependents of the places they occupy;
- ships can support future approach/undock/in-flight/dock presentation without rewriting the logistics authority model;
- removing a mover cannot teleport a ship;
- destroying or disabling a transport cannot permanently strand reservations/contracts/passengers;
- staffing caps and fixed crew bases remain invariant under live experimentation;
- management UI can inspect and issue validated commands without editing internal state;
- inventory rack visuals can be implemented as pure views;
- a fresh multi-day smoke can be reconstructed from durable transition evidence;
- the current colony/farm/water/mining/shuttle content survives long enough to visibly demonstrate the intended loop;
- and none of those repairs require a simulation-backend rewrite.
