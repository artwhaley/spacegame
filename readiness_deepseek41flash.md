# Readiness review — BanishedInSpace (Unity 6000.5.9f1, rev `15fb66b`)

Adversarial, code-grounded review of the current foundation: implementation, scene/prefab/content
wiring, tests, and architecture/authoring documentation. Documentation and acceptance reports were
treated as claims to verify. No changes were made.

## Evidence base and how much to trust it

| Claim source | Status |
|---|---|
| `dotnet build` Runtime/Tests/PlayModeTests | **Verified by me at this revision**: 4 assemblies, 0 warnings, 0 errors (`ColonyPrototype.Editor.csproj` only fails `NETSDK1004` — generated project needs a restore, not a code error) |
| EditMode/PlayMode test execution | **Never executed.** `verify.log` = `[BLOCKED]`; `STAFFING_PATCH_ACCEPTANCE.md` says "Pending licensed Unity run"; `STAFFING_PATCH_BASELINE.md` records a license refusal |
| Manual Play Mode observation | **Exists but is undocumented as evidence**: `Logs/Editor.log` holds 9 session starts, the last running **Day 1 00:00 → Day 6 03:24** with 0 exceptions and 0 authoring-error lines |
| Scene/prefab state | Read as YAML only. Visual/scale claims unverified beyond coordinates |

Two consequences up front:

- **`ARCHITECTURE.md` overstates one lifecycle claim.** It says components register independently
  "without a per-tick scene scan" and `SimulationManager` has "no scene scans during a tick."
  `StaffingManager.SimulationTick` calls `FindObjectsByType<ShipComponent>()` every tick
  (`StaffingManager.cs:670-672`) and `ReconcileColonist` calls `FindReturningShip` →
  `FindObjectsByType<ShipComponent>()` **per non-pilot colonist per tick** (`:428`, `:507`,
  `:605-613`). Doc vs. code mismatch, not a correctness bug.
- **The committed scene contains no runtime state, but a past Play Mode run clearly used a scene
  that did.** At Day 1 00:06 the log shows five colonists ending a duty with `worked 0.00h` and
  **no matching "duty started"** (`Logs/Editor.log:34778, 34817, 34836, 34855`, plus `Pilot 3`),
  and `Pilot 1/2` getting `duty ended: Reassigned` before their first `duty started`.
  `ColonistStatusComponent.EndDuty` returns immediately when `activeDuty == null`, so those records
  had to be **serialized non-null `activeDuty` values at load**. The committed `Person.prefab` and
  scene have `activeDuty: {fileID: 0}`. So that run's scene had runtime duty state baked in — the
  scene file doubles as runtime state, and someone saved the scene during Play Mode. Treat that run
  as evidence of *behaviour*, not as acceptance for the committed baseline.

---

## 1. Ownership and authority

**F1 — Logical containment is implemented as transform parenting; destroying a location destroys its
occupants.** *Severity: High.*
`ColonistAgent.MoveToLocation` does `transform.SetParent(newLocation.transform, true)`
(`ColonistAgent.cs:135-139`). Callers: `PassengerCarrierComponent.TryBoardPassengers` /
`TryUnboardPassengers` (`:105-130`), `ShipComponent.TryBoardResponsiblePilot` (`:131`),
`ReleaseResponsiblePilotToDock` (`:196-206`). So crew/passengers are children of the ship
GameObject, and workers are children of the facility.
*Scenario:* the milestone says "placeholder ships and stations." Deleting the Shuttle to swap in a
real model destroys the pilot and 2–4 passengers; `PopulationManager`/`StaffingManager` shrink
silently via `OnDestroy`, contracts/reservations/manifests keep referencing destroyed agents, and
`DutyHistory` is gone with them. Disabling (not destroying) the ship also hides its crew from the
population and staffing registries while `PassengerCarrierComponent.currentPassengers` still lists
them.
*Class:* verified defect (behavior is exactly this for the Dev requirement "add, disable, adjust,
and re-enable…while experimenting in a running scene").
*Smallest correction:* keep containment explicit (e.g. an owner field / `LocationAnchor` occupants
list) and set transform position without reparenting, or parent colonists to a stable `People` root
and drive their local position. `MoveToLocation` is already the single mutation point — this is the
right place.
*Verify:* a PlayMode test that destroys a ship carrying a pilot and 2 passengers and asserts
population unchanged, carrier drained, contract cancelled/reopened, source reservations released.

**F2 — Per-tick scene scans contradict the documented contract.** *Severity: Medium (dev).* Refs
above. *Class:* verified. *Fix:* use the existing `LogisticsManager.TransportVehicles` registry (it
already exists) or a small ship registry; cache the returning ship. *Verify:* assert zero
`FindObjectsByType` calls during a simulated hour.

**F3 — Two parallel "is the ship crewed" authorities.** `ShipComponent.IsOperationallyCrewed`
(`ShipComponent.cs:44-48`) plus `HasQualifiedPilot`/`HasActivePilotDuty` is what
`TransportVehicleComponent.IsAvailable` (`:36-40`) and `ExtractionMissionController.TryBeginMission`
(`:107`) actually consult. Meanwhile the Pilot `StaffingRoleDefinition` declares
`minimumActiveForOperation = 1` and publishes blockers through `StaffingComponent.PublishPerformance`
(`StaffingComponent.cs:167-198`) into `FacilityPerformanceComponent` — but **no ship has a
`FacilityPerformanceComponent`**, and the Pilot role has `effects: []`. So the generic staffing
blocker contract is dead weight for ships. *Scenario:* a designer adds `FacilityPerformanceComponent`
to a ship expecting "not operational" to gate dispatch; nothing changes. *Class:* plausible risk /
product decision. *Fix:* either delete the ship path through performance or route `IsAvailable`
through it — don't keep both. *Verify:* one test asserting the chosen path.

**F4 — `crewChangeBase` has two writers.** `StaffingManager.Assign` writes it when null
(`StaffingManager.cs:212-213`) even though the doc calls it "fixed once established"; the scene sets
it at `SpaceSim.unity:400000224/210000030`. *Class:* verified, low. *Fix:* establish it in
`ShipComponent.EnsureInitialized` from a serialized value, and reject assignment when null instead of
adopting.

**F5 — Non-serialized id counters next to serialized lists.** `ContractManager.nextContractId`
(private int = 1) and `LogisticsManager.nextDemandId/nextSupplyId` are not serialized, while
`contracts`/`demands`/`supplies` are. `ProjectSettings/EditorSettings.asset:29-30` has
`m_EnterPlayModeOptionsEnabled: 1` with options `0` (both reloads on), so this is **inactive today**;
it collides the moment the team turns on Disable Domain Reload for iteration speed, which this
project's stated goal invites. *Class:* plausible risk. *Fix:* derive next ids from
`max(existing)+1` in `Awake`, or serialize the counters.

---

## 2. Upcoming presentation seams

**F6 — There is no facility/ship/anchor registry and no stable scene identity.** *Severity: High for
the UI milestone.* `LocationAnchor` carries only `displayName` (`LocationAnchor.cs`); content assets
have `stableId` but scene objects do not. `PopulationManager.Colonists`,
`StaffingManager.KnownColonists`, `LogisticsManager.TransportVehicles`, `ContractManager.Contracts`
exist; there is **no** registry of facilities, ships, inventories, stock policies, deposits, or
locations. A colony-wide UI must `FindObjectsByType` and has nothing stable to key a selection or a
save file on. *Class:* verified gap (product decision to defer, but the next milestone needs it).
*Smallest correction:* add `stableId` to `LocationAnchor` (and ship/facility), plus one read-only
registry component (or a `Register`/`Unregister` pair on enable/disable) listing anchors,
`ShipComponent`, `InventoryComponent`, `StaffingComponent`.

**F7 — There is no validated command surface for most UI commands, and the read queries allocate.**
*Severity: High for the UI milestone.* Only `StaffingManager.Assign/Unassign` are validated commands
with an `AssignmentResult` (`StaffingManager.cs:190-260`). Everything else is a raw public field:
stock policy entries (`ResourceStockPolicyComponent.cs:9-40` — including `reorderThreshold`,
`targetStock`, `priority`), `TransportVehicleComponent.disposition/freightEnabled/personnelEnabled`,
`ResourceConverterComponent.activeRecipe`/`operationalEnabled`. There is no "order this ship here"
command and no "select recipe on this facility" with a result. On the read side,
`StaffingComponent.Query`/`CollectWorkers` allocate a fresh `List` per call
(`StaffingComponent.cs:100-104`) and `GetActiveQualifiedWorkers` re-walks all colonists — a per-frame
inspector panel would allocate heavily. *Class:* verified gap. *Fix:* a thin read-only facade
(`TrySetStockPolicy(...) → result`, `TrySetDisposition`, `TrySelectRecipe`, `TryOrderShip`) plus
allocation-free `Refresh*` methods taking caller buffers. Note for whoever writes the UI: `Assign`
synchronously cascades into `ReconcileColonist → FlushCommutes →
ContractManager.CreatePassengerContract → LogisticsManager.TryAssignNext →
TransportExecutor.StartContract → TryClaimMovement` (see `FlushCommutes`, `:779-800`), so a command
has immediate side effects.

**F8 — Movement conflates "logical location" with "physical position"; docking has no port.**
`ShipMovementComponent.MoveToward(LocationAnchor)` moves the transform to the *anchor's origin*
(`ShipMovementComponent.cs:9-20`), and `SetDock` never moves anything. Arrival therefore means
"parked on the facility's anchor," which the scene makes literal: the Farm is at `(12,0,0)`, the
Command Post at the origin, and the shuttle ends up on top of each. Docking ports need a port
transform distinct from the logical anchor. *Class:* verified seam gap (exactly the milestone's
"docking ports" risk). *Fix:* give each dockable location a `DockingPort` anchor (position + facing)
and drive `MoveToward(port)`; keep `CurrentDock` as the logical station.

**F9 — Inventory representation is sound; state the read-only rule now.** `InventoryComponent` is the
single local authority (`Entries`, `Reserve`/`WithdrawReserved`/`Add`/`Remove`) and everything reads
it (converter, contracts, executor, extraction). Racks must be **views of `Entries`**, never their
own `InventoryComponent`. Note the ceiling: discrete resources are whole floats
(`ResourceQuantityRules`), so there is no per-item identity — "boxes on racks" can only show
aggregate counts. *Class:* product decision; smallest contract = "presentation never mutates
inventory; it subscribes to `Entries`."

---

## 3. Lifecycle resilience

**F10 — Destroying a vehicle leaks its contract: freight reservations freeze and passenger contracts
deadlock.** *Severity: High.* `TransportExecutorComponent` has `OnDisable` (unregister only,
`:65-68`) and **no `OnDestroy`**; nothing releases or reopens its contract. `ContractManager.Cancel`
is only reachable from `TransportExecutorComponent.CompleteLoading` and
`LogisticsManager.BuildPassengerCandidate` (`ContractManager.cs:318-338`). `HasActivePassengerFor`
counts any `IsActive` contract (`:150-165`). `LogisticsManager.FindBestCandidate` only considers
`state == Open`.
*Scenario:* delete the Shuttle during a freight run. The contract stays `Assigned` with a Unity-null
`assignedVehicle`; the source's `reserved` never drops (`InventoryComponent.ReleaseReservation` is
only called from `Cancel`); `GetActiveFreightQuantityForDemand` still counts it as inbound, so the
demand never re-requests; and any colonist named in a passenger contract can never be re-commuted
because `QueueCommute` early-returns on `HasActivePassengerContract` (`StaffingManager.cs:752`). The
passenger is stuck `WaitingForTransport` (or `Passenger` if already aboard) forever, and
`PassengerCarrierComponent.currentPassengers` permanently consumes capacity.
*Class:* verified defect.
*Fix:* `TransportExecutorComponent.OnDisable/OnDestroy` (and `ShipComponent` teardown) must release
the movement lease, cancel or reopen the contract, release source reservations, and unboard/return
passengers; `LogisticsManager` should re-offer contracts whose `assignedVehicle` is no longer
available.
*Verify:* PlayMode test — assign a freight contract, destroy the vehicle, assert `reserved == 0`,
contract `Cancelled`/`Open`, and the colonist re-requested.

**F11 — Disabling an operation component creates a stuck "completing committed work + accumulating
fatigue" pilot.** *Severity: Medium-High.* `ShipCrewDutyComponent.HasActiveShipOperation` returns
true whenever `ExtractionMissionController.State != Idle` (`:230-240`) **regardless of whether the
controller is enabled**. `ShipCrewDutyComponent.SimulationTick` then parks in `ReturningHome` waiting
for the operation (`:156-163`), while `StaffingManager.TickResponsibleShipDuties` keeps charging work
time and fatigue because all of its guards still pass (`:670-716`).
*Scenario:* disable `ExtractionMissionController` (or the ship's `operationalEnabled`) while the
Mining Ship is away with a release requested. The mission never advances, the ship never returns, and
the pilot burns to 0.90+ indefinitely. Re-enabling resumes (so it is a pause, not a permanent hang) —
but the *duty phase* claims an active obligation the worker cannot act on.
*Class:* verified defect (this is precisely "pause and resume" vs "cancel and release").
*Fix:* make `HasActiveShipOperation` respect `isActiveAndEnabled`, and treat a disabled operation as
suspended (no fatigue accrual, phase not `CompletingCommittedWork`), or complete/abort it on disable.

**F12 — Manager replacement drops contracts but not in-flight participant state.** A new
`ContractManager` starts empty while colonists keep `activity == Passenger` and carriers keep
`currentPassengers`, so `TransportExecutorComponent` clears its contract (`:100-113`) but the
passengers stay aboard, and `ReconcileColonist` will try to commute them *from the moving ship
anchor* (`:485`). No test covers this. *Class:* plausible risk. *Fix:* on `Instance` replacement (or
a `ContractManager` re-init), reconcile carriers and passengers; a colonist whose contract vanished
should be routed from its actual location.

**F13 — Two authorities add `ShipCrewDutyComponent`.** `ShipComponent.Start` (`:82-84`) and
`StaffingManager.ReconcilePilot` (`:496-497`). *Class:* verified, low. *Fix:* one place.

**F14 — `ReleaseRequested` clears on release.** `ReleaseResponsiblePilotToDock →
ClearReleaseRequest` (`ShipComponent.cs:196-206`), so `ReleaseRequested` is not a durable "why"
record — only the `DutyRecord` is. Fine if documented; state it.

---

## 4. State and time correctness

**F15 — Duty phases overstate readiness and can point the wrong way.** *Severity: High before UI
binds.*
- `TickResponsibleShipDuties` sets `AcceptingNewWork` whenever `!ship.ReleaseRequested`
  (`StaffingManager.cs:704-706`) — i.e. a pilot **flying a committed passenger contract** reports
  "ready for new work." `ReconcilePilot` does the same (`:545`). The enum doc defines
  `CompletingCommittedWork` as *only* the shift/release boundary, so there is no phase meaning "on
  shift and executing committed work."
- `ReconcileColonist` sets `ReturningHome` for any in-transit passenger whose shift is inactive
  (`:410-415`) even when the flight is **toward the workplace** — a UI arrow would point backwards.
*Class:* verified defect of the phase model. *Fix:* set the "executing" phase whenever an operation
is active (or add one enum member), and derive the in-transit phase from the contract's destination
vs. home/workplace rather than from shift activity alone. *Verify:* a test that drives one full
passenger contract and asserts the phase at each stage; a UI snapshot test asserting no colonist
reports a phase contradicting its contract destination.

**F16 — Duty-end reason lies about exhaustion; `Exhausted` is never recorded.** *Severity: Medium
(observability).* Across **all** sessions: `28 ShiftEnded, 4 WorkplaceUnavailable, 2 Reassigned, 0
Exhausted`. Real lines: `Pilot 2 duty ended: ShiftEnded (fatigue 0.90, worked 9.00h)`, `0.92`,
`0.93`. Mechanism: the pilot crosses 0.90 *during the post-shift return flight*, so
`RequestDutyRelease(Exhausted)` no-ops (already requested as `ShiftEnded`,
`ColonistStatusComponent.cs`), and `ReleaseAtBase` writes `ship.ReleaseReason`
(`ShipCrewDutyComponent.cs:207-219`). Exhaustion is invisible in history, and the flagship
"exhaustion triggers at 90%" rule never appears as such. *Fix:* in `ReleaseAtBase`, prefer
`DutyEndReason.Exhausted` when `status.IsExhausted`.

**F17 — Pilots hit the exhaustion threshold on essentially every shift.** *Severity: Medium / product
decision.* `Pilot` role `exertionMultiplier = 1`, `workFatiguePerHour = 0.10`, exhaustion `0.90`: an
8h shift plus the ~1.2h return leg is 9.0–9.3h → 0.90–0.93 every day (log-verified). The latch then
always reads "exhausted" for pilots, which makes the flag useless for them and silently couples shift
length to threshold. Fatigue *does* fully recover between shifts (every `duty started` line shows
`fatigue 0.00`; the A/B/C spacing gives >9h sleep), so this is not an accumulation bug.
*Fix (content):* lower pilot `exertionMultiplier` (~0.85) or exclude the return leg from fatigue at
full rate. *Verify:* a 3-day assertion that pilot fatigue at next shift start is 0 and never reaches
the latch.

**F18 — "Disabling ≠ cancelling" is only half-implemented.** `ResourceConverterComponent`
deliberately retains batch inputs/progress across disable (`:70-78`) — correct.
`ExtractionMissionController.OnDisable` just unregisters, so the `ShipMovementOwner.Extraction` lease
and `Traveling` dock state survive; combined with F11 that is a hazard rather than a clean pause.
`StaffingManager.OnDisable`/`OnEnable` genuinely pause/resume (instance lists survive;
`SimulationManager` re-registers) — good. *Class:* mixed; call out which components are "pause" and
which are "abandon".

**F19 — Shift/midnight handling is correct and empirically confirmed.** `SimulationTime` keeps
absolute hours and derives the 24h view; `ShiftPatternDefinition.IsShiftActive` uses `HourOfDayAt`.
The real run shows `Pilot 1 boarded` at Day 1 00:06, Day 2 00:00, Day 3 00:06, Day 4 00:06, Day 5
00:06, Day 6 00:06 — daily repetition across midnights and **no 16:00 restart**, which is exactly the
pending 24h-clock acceptance gate (satisfied by log evidence, though not by a test run).

---

## 5. Observability

**F20 — Durable evidence is only the Unity Console.** `SimulationLog` is a 100-entry rolling
`List<string>` (`SimulationLog.cs:8-13`) that also `Debug.Log`s; `DutyHistory` caps at 32 records
(`ColonistStatusComponent.maximumDutyRecords = 32`); contracts/demands/supplies are in-memory lists;
`BuildDailyScheduleTable` writes a single `lastScheduleDump` string. Leaving Play Mode discards
everything except `Editor.log`. For "explain a multi-day run," the log is the only artifact — and it
is unbounded text, unbounded in size, with no structure.
*Missing instrumentation:* no facility operational↔blocked transition, no demand planning-status
transition (`UpdatePlanningStates` recomputes `"Satisfied"/"Waiting for transport"/…` every tick at
`LogisticsManager.cs:~500-540` but never logs a change), no reservation change, no location/dock
transition (`MoveToLocation`/`SetDock`/`MarkDeparted` are silent), no fatigue recovery or latch-clear
event, no duty correlation id (only `contractId`/`demandId`).
*Smallest correction:* one Editor-only "dump on demand" (duty history + contracts + demand/supply
tables + per-inventory entries → file), plus transition-only logs for operational state, planning
status, and reservation changes. Do **not** add per-tick logs.
*Verify:* run 3 game days, dump, and answer for every worker/ship/contract "why" from the file alone.

**F21 — Excessive per-tick observational work.** `StaffingComponent.SimulationTick →
RefreshDebugRows` every tick (`:77-79`) allocates one `StaffingDebugRow` per role×shift and runs 4
stage queries × all colonists per row. `FacilityPerformanceComponent` re-runs
`GetComponents<MonoBehaviour>()` on **every** property read (`:132-141`), and
`ResourceConverterComponent` reads it 2–3× per tick (`:126-136`). `LogisticsManager.UpdatePlanningStates`
allocates a status string per demand per tick. *Class:* verified; prototype-tolerable, but this is
the "excessive noise" the brief asks about. *Fix:* gate `RefreshDebugRows` behind `#if UNITY_EDITOR`
and throttle; cache providers with explicit invalidation; compute planning status only on change.

**F22 — Log-quality defects visible in the real run.** (a) `resource` interpolates as
`UnityEngine.Object.ToString()` → `143` occurrences of "Water (AsteroidColony.ResourceDefinition)" in
one session. (b) Raw floats: `delivered 0.6999993`, `10.39997`, `4.009751`. (c)
`LogisticsManager: work waiting - 1 vehicle, none available [Shuttle 1: available]` —
self-contradictory, logged 9× because the vehicle *is* available but had no eligible candidate
(`DescribeUnavailableVehicles` at `LogisticsManager.cs:~560` prints `AvailabilityBlocker()` which
says "available"). (d) `Shuttle 1 delivered … to Command Post` precedes `Shuttle 1 arrived Command
Post` (`BeginUnloading` orders them that way, `TransportExecutorComponent.cs:229-243`). (e)
`SimulationLog.AddEntry` stamps `hour 0` if `simulationManager` is unwired (`SimulationLog.cs:41-44`).
(f) `SimulationLog.Instance` is set unconditionally in `Awake` with no duplicate guard, unlike every
other manager. All low severity, all cheap.

---

## 6. Tests and acceptance

**F23 — Compilation: pass. Tests executed: none.** I built Runtime, Tests, PlayModeTests: clean. No
EditMode/PlayMode run has ever completed in this repo; `verify.log` is `[BLOCKED]`. **The first
"repair" is running the suites** — nothing below substitutes for it, and until it happens the 23
files (~130 test methods) are unproven code.

**F24 — Tests bypass the real integration paths, so the milestone's core loop is untested.**
`PilotDutyTests`, `UnifiedDispatchTests.CreateVehicle`, `TransportCompositionTests`,
`ExtractionCompositionTests`, `TransportExecutorTests` all hand-feed `AdoptResponsiblePilot` +
`BeginPilotDuty` instead of `ShipCrewDutyComponent.TryBoardEligiblePilot`, so they pass even if
boarding, shift gating, or crew-base validation is broken. `FacilityPerformanceTests.ActiveWorker`
sets `currentEmployment`/`activity`/`currentLocation` directly, so `StaffingManager.Assign →
Reconcile → performance` is never exercised as a chain.
`ExtractionCompositionTests.UnloadBlockageKeepsCargoOnShip` writes private `state` by reflection to
manufacture an intermediate state. `NoCodeAuthoringProofTests` and most inventory setups inject
private `entries` by reflection, bypassing `InventoryComponent`'s normalization/serialization
boundary. Nothing completes a freight or passenger contract end-to-end, nothing flies a ship home
after release (the one test that looks like it releases happens to be *at* base, so `CurrentDock ==
returnDestination` short-circuits the flight), and nothing drives a colonist home→shuttle→workplace
under `SimulationTick`. *Class:* verified. *Recommend (only these):* one PlayMode "one full day"
scenario asserting the loop's invariants (Food/Water not zero, contracts complete, no reservation
leak, every duty ended with a truthful reason, all ships back at base); one "destroy/replace a ship"
lifecycle test (F1/F10); one contract-driven duty-phase test (F15); plus the existing pure unit
tests, which are good.

**F25 — PlayMode "lifecycle" tests don't test our components.** `RuntimeLifecyclePlayModeTests` uses
a toy `LifecycleProbe`/`OrderingProbe`; it validates `SimulationManager` registration/priority/
replacement and nothing about a real facility, ship, converter, or policy surviving disable/enable.
*Class:* verified. *Fix:* keep them (they do protect the tick contract) and add one real-component
toggle test.

**F26 — Brittle assertions.** `StaffingManagerTests.DailyScheduleTableIncludesAssignedAndUnassignedColonists`
asserts the exact header and exact window strings; `StaffingTests.DuplicateAndNullDefinitionsAreReported`
asserts `errors.Count == 4`; `SimulationTimeTests` asserts exact timestamp strings. Presentation
contracts are worth pinning, but pin *one* canonical snapshot, not a dozen substrings scattered
across tests.

---

## 7. Fit for the small playable slice

**F27 — The Food loop is not survivable, so the farm→shuttle→depot loop can't be observed as a causal
system.** *Severity: High for the slice.* Command Post consumes `0.1 Food/resident/hour × 9 residents
= 21.6 Food/day` (`SpaceSim.unity:400000204`). The Farm's ceiling with 2 Shift-A farmers is `1.0
multiplier × ~7h = ~7–8 Food/day` (`HydroponicFood.asset` 0.5 Water → 1 Food per 1h;
`FarmOperator.asset` curve `[0, 0.65, 1.0, 1.2]`; both farmers on shift A only). Splitting to A+B
yields 10.4/day — still half of demand. **This is confirmed in the real run, not just arithmetic:**
the first `Food shortage at Command Post - inventory empty` occurs at **Day 1 13:24**, 44 times
across sessions (13 in the last), and the Food economy never recovers. Water is marginal too (~24
Water/day mined+processed vs ~25.6/day demand including the Farm's 0.5 Water/Food input), with 4
Water shortages observed. Because there is no starvation consequence, the Food loop is effectively
decorative within one game day. *Class:* product/tuning decision (the architecture is fine).
*Smallest correction:* content tuning — e.g. `amountPerResidentPerGameHour ≈ 0.04` (≈8.6/day,
comfortably under A+B output), or add a third Farm Technician on Shift C, or raise farm throughput.
*Verify:* a 72h headless assertion that Food never reaches 0 and that Farm→shuttle→depot deliveries
are the reason.

**F28 — Placeholder scale/placement is incoherent and partly logically wrong.** The Mining Ship
transform is `(0,0,10)` — the **same position as the Water Processor** (`SpaceSim.unity:210000022` vs
`:210000002`) — while its `initialDock`/`crewChangeBase` is the Command Post at the origin. So its
logical dock and its visual position disagree from frame one. Facility visuals are primitives
(Cube/Sphere/Capsule); the Command Post (0,0,0), Farm (12,0,0), Processor (0,0,10), Asteroid
(24,0,10). Colliders exist only on the Farm's child cube (`:594`), the Shuttle child (`:1870`), and
the Command Post child (`:2345`) — the Water Processor, Mining Ship and Ice Asteroid have none. There
is no `EventSystem`. *Consequence:* selection works only for three objects via raycast, and "coherent
scale and placement" is not met. *Class:* verified; intended to be fixed as part of this milestone.
*Fix:* a shared dock/port convention (F8) and one pass over placeholder positions/scales with
colliders on every selectable.

**F29 — No save/load seam, and the scene doubles as runtime state.** There is no persistence code
anywhere (only `Assets/TutorialInfo/.../ReadmeEditor.cs` uses `System.IO`). Runtime-only state that
scene serialization cannot capture: `ResourceConverterComponent.batchActive/batchProgress`
(non-serialized, `:41-42`), `nextContractId/nextDemandId/nextSupplyId`, `ResourceStockPolicyEntry`'s
`[System.NonSerialized]` demand/supply ids, `SimulationManager`'s static registries, and all
in-flight ship positions/docks/duty records. Meanwhile the *authored* scene already stores
`currentEmployment`, `home`, `currentLocation`, `fatigue` — and the log proves a Play Mode save once
baked live `activeDuty` records into it. *Class:* verified/architectural, deliberately out of scope.
*Recommendation:* don't build save/load now, but (a) never save the scene during Play Mode, and
(b) make the shape saveable with two tiny changes — serialize converter batch state and derive the id
counters.

**F30 — Pause/speed exist and are adequate.** `paused`, `speedMultiplier`, `gameHoursPerRealSecond`,
`tickIntervalSeconds` are public and drive `Update` (`SimulationManager.cs:120-135`); `paused`
correctly stops the accumulator without accruing debt. No step-mode and no pause notification; fine
for now, but the UI will want to read `paused` and to force a single tick.

---

## Verdict

**Ready with targeted repairs.** The foundation is genuinely good and I would not accept a rewrite:
single-authority clock (`SimulationManager` + `SimulationTime`), single-authority inventory
(`InventoryComponent`), explicit `EmploymentAssignment`, staffing published as
`IFacilityPerformanceProvider` so recipes stay staffing-free, count-based role curves with no slot
semantics, one deterministic dispatch arbitration, normalized discrete quantities, and a tick
registration contract that actually survives late creation and manager replacement. It
**demonstrably runs 6 game days with no exceptions, no deadlocks, 40 contracts completed, correct
daily shift repetition, correct crew handover and crew-only returns, and correct "pilot flying home
still accrues fatigue."** That is real, logged evidence — I am not going to pretend it doesn't count
just because the test suites never ran.

What is *not* ready is the surface the next milestone lands on: there is no stable scene identity, no
read-only registries, no validated command facade, no dock-port concept, logical containment is
transform parenting, contracts leak when a vehicle dies, the duty phases overstate readiness, and the
Food loop is broken in practice. Those are small, concrete, dependency-ordered repairs — not a
framework.

### Required before the playable slice (dependency-ordered)

1. **Run the suites in a licensed editor and record totals.** No substitutes. (Blocks the credibility
   of everything else.)
2. **Decouple logical containment from transform parenting** (F1) — required by *both* "physical
   colonist transitions" and "placeholder ship/station replacement."
3. **Make vehicle teardown cancel/reopen and release** (F10/F12) and make `HasActiveShipOperation`
   enablement-aware (F11). This is the direct prerequisite for the stated Dev workflow.
4. **Stable ids + read-only registries** for anchors/facilities/ships/inventories (F6), then the
   validated command facade and allocation-free queries (F7). Selection, inspection, and management
   UI all sit on this.
5. **Fix the duty-phase semantics** (F15) and the duty-end reason (F16) *before* any UI binds to
   `ColonistDutyState`.
6. **Tune the Food/Water loop to a stable equilibrium** (F27) — otherwise the slice cannot
   demonstrate its own loop.
7. **One pass over placeholder placement/scale/dock-vs-transform** (F8/F28) as part of the
   ships-and-stations milestone.
8. **One durable dump + transition-only logs, and throttle the per-tick debug refresh** (F20/F21).

### Safe to do while building the slice

F21 (throttle/gate debug rows, cache providers), F22 (log formatting, the contradictory "none
available [available]" message, end-reason, unwired-timestamp), F23's follow-up (first real test
run), F24/F25 (the three integration tests above), F26 (collapse brittle assertions), F5/F4/F13/F14/
F17 (cheap hardening), and correcting the two doc claims in `ARCHITECTURE.md` that per-tick scene
scans don't happen.

### Deliberately leave alone

The converter/recipe vs. staffing split; count-based role curves with no ordered slots; exactly one
`EmploymentAssignment` per colonist; no automatic vacancy filling or call-ins; the unified dispatch
arbitration and its priority/disposition rules; `ResourceQuantityRules` normalization; the tick
priority scheme; `SimulationTime` as the only calendar helper; extraction staying outside
`TransportContract`; and `SimulationLog` as a rolling dev tool — do not turn it into a telemetry
framework, and do not log per tick.

### Remaining unknowns (need Unity or unavailable artifacts)

- EditMode/PlayMode results — never executed; licensing blocked twice.
- Why the last recorded run had serialized `activeDuty` records (five `duty ended …, worked 0.00h`
  with no `duty started`) while the committed scene/prefab have `activeDuty: {fileID: 0}`. Either
  that session predates the committed state or a Play Mode save baked it in; a fresh instrumented run
  with a scene hash printed at startup would settle it.
- Actual visual result — I read YAML only; overlap, hierarchy and readability are unverified.
- Real Food/Water equilibrium over 24–72h under automation (the log already shows the shortage, but
  not whether tuning fixes it).
- Whether `FacilityPerformanceComponent`'s provider rebuild order matters for future multi-provider
  channels; whether `OnValidate` authoring errors spam on load (none in this log).
- Whether the 24h-clock acceptance gate's *fresh* run reproduces the daily boarding pattern the old
  log shows (expected, but that is not a test pass).
