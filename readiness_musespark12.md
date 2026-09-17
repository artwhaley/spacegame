# Adversarial Readiness Review — BanishedInSpace (spacegame)

**Repository:** https://github.com/artwhaley/spacegame  
**Branch:** `main`  
**Review baseline:** `15fb66ba52ea210d57f593acc4ce4d70ce618a31` — *Implement 24-hour staffing, fatigue, and duty phases*  
**Local checkout verified:** `C:/Users/artwh/BanishedInSpace` — `git rev-parse HEAD` = `15fb66b` on `main`, working tree clean, `origin/main` up to date  
**Review date:** 2026-09-16 (UTC)  
**Reviewer stance:** Code-grounded, adversarial. Documentation and acceptance reports treated as claims to verify. Historical tickets distinguished from current behavior.

> **Constraint honored:** This review was performed against the actual source at the stated commit. No architecture substitution based on description alone. The project’s deliberate choice of a network of independent Unity `MonoBehaviour`s over a separate pure-C# simulation backend is treated as a design requirement — ability to add/disable/adjust/re-enable components in a running scene must be preserved. Recommendations are minimum repairs only; no framework rewrite.

---

## Table of Contents

1. [Verdict](#verdict)
2. [How This Review Was Performed](#how-this-review-was-performed)
3. [Ownership and Authority](#1-ownership-and-authority)
4. [Upcoming Presentation Seams](#2-upcoming-presentation-seams)
5. [Lifecycle Resilience](#3-lifecycle-resilience)
6. [State and Time Correctness](#4-state-and-time-correctness)
7. [Observability](#5-observability)
8. [Tests and Acceptance](#6-tests-and-acceptance)
9. [Fit for the Small Playable Slice](#7-fit-for-the-small-playable-slice)
10. [Dependency-Ordered Repairs Before the Slice](#dependency-ordered-repairs-before-the-slice)
11. [Safe to Address While Building the Slice](#safe-to-address-while-building-the-slice)
12. [Deliberately Leave Alone](#deliberately-leave-alone)
13. [Remaining Unknowns Requiring Running Unity / Unavailable Artifacts](#remaining-unknowns-requiring-running-unity--unavailable-artifacts)
14. [Source Index (Reviewed Revision)](#source-index-reviewed-revision)

---

## Verdict

### Ready with Targeted Repairs

**Reasoning:** The foundation is *coherent and deliberately separated* — `InventoryComponent` (stock authority), `StaffingManager.currentEmployment` (employment authority), `ColonistAgent.currentLocation` (physical location authority), `ShipComponent.movementOwner` lease (movement authority), `PassengerCarrierComponent.currentPassengers` (passenger membership), `ColonistStatusComponent` (fatigue/duty/time authority) — all accessed through narrow, validated contracts. The 24-hour clock, three-window `Daily8HourShifts` content, count-based staffing curves, foreground/background logistics arbitration, and extraction-as-separate-path are sound. The `desiredTickables` static registry fixes the historical lifecycle flaws.

**But 4 defects/gaps will bite the slice immediately** if not fixed first:

1. **Disabled workers don’t count toward shift capacity** → silent overstaffing to 4 on a max-3 shift after an Inspector disable/re-enable.
2. **Passenger contracts leak when a colonist or carrier is destroyed mid-flight** → soft-locked shuttle, never-completing contract, logistics “no vehicle available” forever.
3. **Inventory has no change notification** → “visible boxes on racks” either polls every frame or becomes a second inventory authority that drifts.
4. **Observability is rolling-only + missing contract correlation** → after a 48-hour run you cannot answer “which demand was this pilot servicing” and early `SimulationLog` evidence has wrapped.

Apply the 7 item ordered repair list (§10). Do not rewrite the architecture. Do not add save/load, auto-staffing, rerouting, or universal work-orders.

**If repairs are skipped:** Expect stuck shuttles, phantom over-capacity shifts, rack visuals that desync under batch discrete adds, and post-mortems that cannot explain day-1 behavior — all while the game *appears* playable for the first few hours.

---

## How This Review Was Performed

1. **Revision confirmation:** `git rev-parse HEAD` and `git log --oneline -10` against local clone at `C:/Users/artwh/BanishedInSpace`. Confirmed `15fb66b` is `HEAD`.
2. **Implementation read:** All 42 runtime `Assets/Scripts/ColonyPrototype/**/*.cs`, `Assets/SpaceSim.unity` (YAML), `Assets/Person.prefab`, `Assets/GameData/Shifts/Daily8HourShifts.asset` + all `GameData` content, 21 EditMode + 1 PlayMode test files.
3. **Documentation cross-check:** `ARCHITECTURE.md`, `STAFFING_ARCHITECTURE.md`, `STAFFING_AUTHORING.md`, `STAFFING_BASELINE.md`, `STAFFING_PATCH_BASELINE.md`, `STAFFING_PATCH_ACCEPTANCE.md`, `CONTENT_AUTHORING.md`, plus `tickets/24_Hour_Day_Clock_Patch/**` and `tickets/Staffing_Fatigue_Immediate_Assignment_Patch/**` locked design contracts.
4. **Pattern searches:** `code_search` for `ColonistDutyState`, `DutyEndReason`, `SetDutyState`, `MovementOwner`, `HasActiveShipOperation`, `FindReturningShip`, etc., to trace authority and duty-phase consistency.
5. **Claim verification:** Every doc claim checked against the referenced source line; “pending licensed Unity run” in acceptance docs was verified as *still pending* — source compiles (`dotnet build` 3 projects, 0 errors, per acceptance log) but Edit/PlayMode totals have not been executed in a licensed batch-mode run.

---

## 1. Ownership and Authority

### Canonical Owners (verified)

| Concern | Owner | Key Fields / API |
|---|---|---|
| **Inventory** | `InventoryComponent` per GO | `onHand`, `reserved`, `capacity`, `Available`; `Reserve`/`WithdrawReserved`/`Add`/`Remove`; discrete normalization at every mutation boundary |
| **Employment** | `StaffingManager` + `ColonistAgent.currentEmployment` | `EmploymentAssignment { workplace, role, shiftId }`, `Assign`/`Unassign` → `AssignmentResult`, atomic immediate mutation, `CountAssigned` |
| **Physical location** | `ColonistAgent` | `currentLocation: LocationAnchor`, `MoveToLocation()` (parents transform), read by `StaffingComponent.MeetsStage`, `PassengerCarrierComponent` |
| **Ship movement** | `ShipComponent.movementOwner` lease | `ShipMovementOwner { None, Transport, Extraction, CrewReturn }`, `TryClaimMovement`/`ReleaseMovement`, `MarkDeparted`/`SetDock` |
| **Passenger membership** | `PassengerCarrierComponent.currentPassengers` + `TransportContract.passengers` + `ShipComponent.ResponsiblePilot` (temporary lease) | `TryBoardPassengers`/`TryUnboardPassengers`, `ShipCrewDutyComponent.ResponsiblePilot` |
| **Duty / fatigue / time** | `ColonistStatusComponent` + `SimulationManager` | `fatigue 0..1`, `IsExhausted` latch (0.90 set / 0.20 clear), `DutyRecord` (32 cap), `CurrentGameHour` monotonic / `SimulationTime.HourOfDayAt` calendar view |

**Overall assessment:** Separation is disciplined. `ResourceConverterComponent` never reads worker counts; `StaffingComponent` never knows Food/Water/recipes; `RecipeDefinition` never contains staffing; `ResourceStockPolicyComponent` never picks a source.

---

### F-O1 — Shift capacity counts disabled workers as free — **Verified Defect — HIGH**

- **Severity:** High. **Consequence (player):** Overstaffed shift produces clamped multiplier but silently violates explicit `maximumAssignedPerShift=3`; player sees 4 assigned in `DebugRows` with no rejection. **Consequence (developer):** Authoring constraint becomes unenforceable during Inspector disable/re-enable experimentation — the exact workflow the architecture promises to support.
- **Source references:**
  - `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs:322` — `CountAssigned` filters `colonist.isActiveAndEnabled`
  - `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs:268` — `ValidateAssignment` → `RejectedAtCapacity` check uses `CountAssigned`
  - `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs:146` — `ReconcilePopulation` prunes `!isActiveAndEnabled` from `knownColonists`
  - `Assets/Scripts/ColonyPrototype/People/StaffingComponent.cs:98` — `CollectWorkers` delegates to `StaffingManager.CollectAssignedWorkers` (also filters `isActiveAndEnabled`)
- **Scenario to expose:**
  1. Farm 1 has `maximumAssignedPerShift=3`, 3 farmers assigned to Shift A (verified via `StaffingComponent.DebugRows`).
  2. In Play Mode, disable `Farmer 2` GameObject (Inspector checkbox) to test “what if we lose one worker”.
  3. `StaffingManager.Assign(Farmer 4, farm, FarmOperator, "A")` now succeeds — disabled worker not counted.
  4. Re-enable `Farmer 2` → 4 persistently employed workers on a 3-cap shift. Curve clamps to `multiplierByActiveCount[3]=1.2` but the invariant is broken with no diagnostic.
- **Verified vs plausible:** **Verified defect** — logic inspection + existing lifecycle code proves the path.
- **Smallest correction:** Count *employed* regardless of active state for capacity. Keep pruned colonists in a shadow set or change `CountAssigned`/`CollectAssignedWorkers` to count `currentEmployment != null` without `isActiveAndEnabled` check (still skip null employment). Pruning from `knownColonists` for tick purposes should not affect capacity accounting. Alternative minimal: cache `employedCountEvenIfDisabled` and use it in `ValidateAssignment`.
- **Verification:** New EditMode test `DisabledWorkerStillCountsTowardCapacity`:
  ```csharp
  // Assign 3 to A (max 3), disable one GO, attempt 4th → RejectedAtCapacity
  // Re-enable → still 3, no overcount; DebugRows shows 3 assigned
  ```

---

### F-O2 — Passenger dual-authority leaks on destroy — **Plausible Risk escalating to Defect — HIGH**

- **Severity:** High. **Consequence:** Soft-locked shuttle, `Contract.state` forever `TravelingToPickup` or `Unloading` never reached, `LogisticsManager.ReportWaiting("none available")` spam, demand never cleared, farm starves.
- **Source references:**
  - `Assets/Scripts/ColonyPrototype/Vehicles/PassengerCarrierComponent.cs:55-73` — `TryBoardPassengers` parents immediately; `TryUnboardPassengers` requires *every* passenger at `CarrierLocation` or returns `false` (no partial progress)
  - `Assets/Scripts/ColonyPrototype/Vehicles/TransportExecutorComponent.cs:137` — early `return` when `!Vehicle.isActiveAndEnabled` — no cancellation path for destroyed passengers
  - `Assets/Scripts/ColonyPrototype/Logistics/ContractManager.cs:HasActivePassengerFor` — loops `c.passengers[j]==colonist` (null not skipped explicitly, but Unity `==` handles destroyed; still loop never cleans)
  - `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs:413` — `HasPassengerInTransit` checks `activity==Passenger && HasActivePassengerFor`
- **Scenario to expose:**
  1. Two farmers on Shift A grouped into one passenger contract (capacity 4) from `Command Post` → `Farm 1`.
  2. Shuttle `TravelingToPickup` (within 10m at 12 m/s, 0.1h tick). Destroy `Farmer 1` GO (simulate death or scene reload) while `CarrierLocation` is the shuttle.
  3. `TryUnboardPassengers` at `Farm 1` fails because one passenger is null/destroyed; `TransportExecutor.SimulationTick` at `Unloading` returns without `CompleteCurrentContract`; contract stays `IsActive=true`; `ContractManager.HasActivePassengerFor(Farmer 2)` still true so `StaffingManager` keeps `Farmer 2` as `Passenger` forever.
- **Verified vs plausible:** **Plausible risk with verified code path** — would manifest in any playthrough where a colonist is destroyed or a carrier disabled mid-contract. The “missing carrier” case is partially handled (`ShipCrewDutyComponent` force-releases stale `MovementOwner` at line 190 when `!HasActiveShipOperation()`), but passenger manifest corruption is not.
- **Smallest correction (10 lines):**
  - `PassengerCarrierComponent.TryUnboardPassengers` / `CanBoardPassengers`: skip null, prune nulls from `currentPassengers` on `OnDisable`.
  - `ContractManager.HasActivePassengerFor` / `HasActivePassengerForAny`: skip `passengers[j]==null`.
  - `TransportExecutor.SimulationTick`: if `CurrentContract.type==Passenger` and every passenger is null/destroyed, `Cancel(CurrentContract)` with log “all passengers lost”, release movement lease, `TryAssignNext()`.
- **Verification:** EditMode test `ContractCancelledWhenPassengersDestroyedMidFlight` — create 2-passenger contract, `DestroyImmediate` one colonist, tick executor → contract cancelled, `sourceInventory` reservation (N/A for passenger) not leaked, new contract can be assigned, `SimulationLog` contains “cancelled”.

---

### F-O3 — Movement lease is sound but implicit contract — **Product Decision — MEDIUM**

- **Severity:** Medium. **Consequence:** Future docking maneuvers that add a second `LocationAnchor` will break `ShipComponent.CurrentDock` serialization if treated as a raw anchor field.
- **Source references:**
  - `Assets/Scripts/ColonyPrototype/Vehicles/ShipComponent.cs:140-162` — `TryClaimMovement`/`ReleaseMovement`, `SetDock`, `MarkDeparted`, `currentDock` serialized
  - `Assets/Scripts/ColonyPrototype/Vehicles/ShipCrewDutyComponent.cs:190` — forced `ReleaseMovement` when `!HasActiveShipOperation()` (stale lease recovery)
  - `Assets/Scripts/ColonyPrototype/Vehicles/TransportExecutorComponent.cs:102` — `TryClaimMovement(Transport)` before departure
  - `Assets/Scripts/ColonyPrototype/Extraction/ExtractionMissionController.cs:118` — `TryClaimMovement(Extraction)`
- **Scenario:** Adding a second docking port GO without changing `ShipComponent` → two anchors both write `currentDock`, last write wins, pilot boards at wrong port.
- **Smallest correction (now, non-breaking):** Introduce `IDockingSurface { LocationAnchor Anchor { get; } }` and make `ShipComponent.CurrentDock` a proxy to `GetComponent<IDockingSurface>()?.Anchor ?? currentDock`. No serialized change yet — just the seam so `DockingPortComponent` can be added on the slice without rewriting consumers.
- **Verification:** Add `DockingPortComponent` test object, assert `ShipComponent.CurrentDock` resolves to port anchor when present, falls back to serialized `currentDock` otherwise.

---

## 2. Upcoming Presentation Seams

### Seams That Already Exist (verified)

| Need | Seam | File |
|---|---|---|
| UI inspect facility state | `FacilityPerformanceComponent.IsOperational`, `BlockSummary`, `GetMultiplier(effect)`, `StaffingComponent.DebugRows` | `FacilityPerformanceComponent.cs`, `StaffingComponent.cs` |
| UI issue validated commands | `StaffingManager.Assign` → `AssignmentResult`, `ResourceConverterComponent.SelectRecipe` | `StaffingManager.cs:188`, `ResourceConverterComponent.cs:46` |
| Ship readiness | `ShipComponent.ReadinessBlocker()` | `ShipComponent.cs:165` |
| Dock safety | `ShipComponent.HasSafeDock`, `IsTraveling`, `IsOperationallyCrewed` | `ShipComponent.cs:28-30` |
| Freight policy | `ResourceStockPolicyComponent.entries[]` (Inspector-editable) | `ResourceStockPolicyComponent.cs` |

All of these are read-only queries or validated commands; UI does not need to edit internal fields.

---

### F-P1 — No inventory change notification for “boxes on racks” — **Verified Gap — HIGH**

- **Severity:** High for the slice. **Consequence:** Rack visuals must poll `InventoryComponent.GetOnHand` every frame or duplicate authority by caching `onHand` themselves; under batched discrete adds (e.g., `ResourceCollectorComponent` adding 3 wrenches via accumulator), poll interval can miss intermediate states or rack instances drift.
- **Source references:** `Assets/Scripts/ColonyPrototype/Economy/InventoryComponent.cs:90-160` — `Add`/`Remove`/`Reserve`/`WithdrawReserved`/`ReleaseReservation` mutate `onHand`/`reserved` and call `entry.Refresh()` but fire no event.
- **Scenario:** Water `onHand` goes 0 → 8 in one tick (freight unload + converter batch emit). Rack view polling at 0.1s real-time sees only the final 8, never spawns the 4 intermediate boxes, or spawns 8 then immediately destroys 4 when converter consumes — visual flicker.
- **Smallest correction:** Add `event Action<ResourceDefinition, float, float> OnInventoryChanged` (resource, oldOnHand, newOnHand) fired once per successful mutation after `Refresh()`. Rack view subscribes `OnEnable`, unsubscribes `OnDisable`, never writes inventory — view-only.
- **Verification:** EditMode test subscribes, `Add(Water,5)` → event fires once with delta +5; fractional discrete `Add(Wrench,0.2)` → rejected with error and no event (existing behavior preserved).

---

### F-P2 — Single `CurrentDock` assumes one port — **Risk — MEDIUM**

Covered as F-O3. Additional note for racks: the same discipline applies — racks must be *views*, not *inventories*. Do not add `InventoryComponent` to a rack.

---

### F-P3 — Colonist logical vs visual arrival — **Risk — MEDIUM**

- **Severity:** Medium. **Consequence:** If “bed → seat → workplace” interpolates `currentLocation` over time, `FacilityPerformanceComponent` (which reads `currentLocation == workplaceLocation` at `EvaluateAt` time) will see late arrivals as present only after the animation finishes → farmers lose the first 0.2h of every shift.
- **Source references:**
  - `Assets/Scripts/ColonyPrototype/People/ColonistAgent.cs:112` — `MoveToLocation` updates `currentLocation` and `SetParent` atomically
  - `Assets/Scripts/ColonyPrototype/People/StaffingComponent.cs:162` — `MeetsStage` checks `worker.currentLocation != workplaceLocation`
  - `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs:453` — `ReconcileColonist` checks `colonist.currentLocation == workplace` to set `Working`
- **Scenario:** Farmer `WaitingForTransport` at `Command Post`, shuttle `MoveToward` completes, `TryUnboardPassengers` parents to `Farm 1` and sets `Idle`. Current code works because logical arrival is instant. A future `ColonistVisualMover` that lerps `currentLocation` would delay `Working`.
- **Smallest correction:** Keep `currentLocation` immediate (logical). Add visual-only `ColonistVisualMover : MonoBehaviour` that lerps `transform.position` toward `currentLocation.transform.position` over ~0.2 game-hours. No change to `StaffingManager` or `StaffingComponent`.
- **Verification:** PlayMode test asserts `colonist.currentLocation==farm` immediately after `TryUnboard`, while `transform.position` is still interpolating (distance > `ArrivalEpsilon`).

---

## 3. Lifecycle Resilience

### What Was Repaired Since Baseline (verified)

Pre-staffing baseline (`STAFFING_BASELINE.md` + `SOURCE_AUDIT_SNAPSHOT.md` era) had:
- Manual `StaffingComponent.assignedWorkers` list (Inspector-managed, no validation)
- `WorkScheduleComponent` 16h cycle (drifted vs 24h day)
- `RecipeStaffingRule` inside recipes (welded workstation to worker type)
- Tickables registered from `Start` (lost when manager appeared later)

Current (`15fb66b`) has:
- **Static `desiredTickables` registry** — `SimulationManager.RegisterTickable`/`UnregisterTickable` work even when `Instance==null`; `AdoptDesiredTickables` on `Awake`/`OnEnable`; `PruneDesiredTickables` removes destroyed `Behaviour`s via `IsLiveEnabled` (Unity `== null` overload).
- **Mutation-safe tick iteration** — `QueueAdd`/`QueueRemove` + `pendingAdds`/`pendingRemoves` + `SortTickables` by `SimulationTickPriority` then `registrationOrdinals`.
- **Priority ordering** — `100 StaffingManager` → `200 StaffingComponent/ResourceConverter/PopulationConsumer` → `300 ResourceStockPolicy/LogisticsManager` → `400 TransportExecutor/ExtractionMissionController` → `1000 default`. Ensures fatigue/activity reconciled before output.
- **Manager replacement** — `PopulationManager`, `StaffingManager`, `LogisticsManager` all `Discover*` via `FindObjectsByType` on `Awake`/`OnEnable`; `ResourceStockPolicyComponent` resets `foregroundDemandId=0` on manager replacement.
- **`ResourceConverterComponent.OnDisable` retains `batchActive`/`batchProgress`** — “pause and resume” vs “cancel and release” is correctly distinguished for batches; disabling pauses work, re-enabling resumes the same batch.

Verified by `Assets/Tests/PlayMode/RuntimeLifecyclePlayModeTests.cs: ComponentCreatedBeforeManagerIsDiscovered`, `ComponentCreatedAfterManagerIsDiscoveredAndManagerReplacementResumes`, `TickPriorityRunsLowerNumbersFirst`.

---

### F-L1 — Covered as F-O1 (disable/re-enable capacity leak)

### F-L2 — Domain-reload statics — **Risk — LOW**

- **Source:** `Assets/Scripts/ColonyPrototype/Core/SimulationManager.cs:19-21` — `desiredTickables` static + `registrationOrdinals`.
- **Scenario with “Enter Play Mode without Domain Reload”:** Statics survive domain reload; `Instance` is destroyed but `desiredTickables` retains dead `Behaviour` refs from previous Play Mode. `PruneDesiredTickables` via `IsLiveEnabled` (checks `behaviour.isActiveAndEnabled`) correctly removes them on next `AdoptDesiredTickables`. **No change needed**, but the pending PlayMode test should enable the no-domain-reload option to prove it.
- **Verification:** Run `RuntimeLifecyclePlayModeTests` with the editor option toggled; assert `desiredTickables.Count==0` after destroying all probes.

### F-L3 — Manager replacement demand-ID reset is correct but observable — **LOW**

- **Source:** `Assets/Scripts/ColonyPrototype/Logistics/ResourceStockPolicyComponent.cs:63` — `foregroundDemandId=0` when `registeredManager != logistics`.
- **Consequence:** Old freight contracts with `demandId==0` are ignored by `ContractManager.GetActiveFreightQuantityForDemand` (returns 0) — harmless, but logistics will log “waiting for source” while old contracts still show as `IsActive`. No fix before slice; document as expected.

---

## 4. State and Time Correctness

### Clock — Verified Sound

- `SimulationManager.CurrentGameHour` is monotonic absolute elapsed hours, never wraps. `CurrentDayIndex = Floor(hour/24)`, `CurrentDayNumber = index+1`, `CurrentHourOfDay = positive modulo` via `SimulationTime.HourOfDayAt` (`STAFFING_ARCHITECTURE.md:91` and `SimulationTime.cs:28`).
- `OnValidate` clamps `NaN`/`Infinity`/negative to 0. Pure helper remains deterministic for negative test input (`SimulationTimeTests:NegativePureClockInputUsesPositiveModulo`).
- `ShiftDefinition.ContainsHourOfDay` handles `duration>=24 → true` and midnight wrap (`start 20 dur 8` covers `20:00-24:00` and `00:00-04:00`). `ShiftPatternDefinition.IsShiftActive` reduces absolute hour via `HourOfDayAt` then tests the window — so Shift A at 0h and again at 24h is the *same daily window* on consecutive days (not a second trigger at 16h).
- `Daily8HourShifts.asset` GUID preserved, content = A 0..8, B 8..16, C 16..24 — canonical. No one assigned to C post-migration (as specified).
- Verified by `SimulationTimeTests:HourOfDayUsesTwentyFourHourBoundaries`, `StaffingTests:ShiftA/B/CIsActiveForDailyWindow`, `PilotDutyTests:DailyPilotShiftDoesNotRestartAtSixteenHours`.

### Fatigue — Verified Sound

- Rates: `+0.10/h * exertionMultiplier` (working), `-0.10/h * restfulnessMultiplier` (sleeping). Defaults in `ColonistStatusComponent: workFatiguePerHour=0.10, sleepRecoveryPerHour=0.10, exhaustion=0.90, recovered=0.20`, `HabitationComponent.restfulnessMultiplier=1.0`, `StaffingRoleDefinition.exertionMultiplier=1.0`.
- Latch: `RefreshExhaustionLatch` sets `exhausted=true` at `>=0.90`, clears only at `<=0.20` (`ColonistStatusTests:ExhaustionLatchClears`). Applied via `ApplyWorkFatigue`, `ApplySleepRecovery`, `AdjustFatigue` — all clamp and update latch consistently.
- Activity-gated: Only `Working` increases; only `Sleeping` at `home` decreases. `WaitingForTransport`, `Passenger`, `Resting`, `Idle`, `OnDutyCrew` (facility workers) do not change fatigue. `OnDutyCrew` for pilots *is* work and uses `pilotRole.exertionMultiplier` (`StaffingManager.TickResponsibleShipDuties`).

---

### F-S1 — Pilot fatigue *during* return-home is correct but under-tested — **Verified — MEDIUM**

- **Severity:** Medium. **Consequence (player):** Pilot at 0.85 boards, shift ends mid-flight, still accrues fatigue during 2h return; hits 0.90 mid-return → exhaustion latch sets but return *continues* (correct); next assignment blocked until ≤0.20, which may strand the ship for a full sleep cycle — player sees “pilot exhausted during flight home” and cannot board replacement until recovery.
- **Source references:**
  - `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs:420` — `TickResponsibleShipDuties` does `AddWorkedTime(delta)` + `ApplyWorkFatigue(delta, dutyRole.exertionMultiplier)` even when `ship.ReleaseRequested && !HasActiveShipOperation()` (i.e., `ReturningHome` duty state)
  - `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs:388` — `TickPilot` sets `ReturningHome` only when `ReleaseRequested && !HasActiveShipOperation`
  - Spec: “A pilot finishing accepted work and flying home is still working and accumulates fatigue” — **implemented**.
- **Scenario to expose:** Pilot `fatigue=0.85`, `ShipCrewDutyComponent.RequestRelease(ShiftEnded)` during `TravelingToDestination` (freight) or `ReturningHome` (empty). Advance 2h at 1.0 exertion → `fatigue=1.05 → clamp 1.0`, `IsExhausted=true`, `ship.ReleaseRequested` remains true, `CrewReturn` movement continues, duty state `ReturningHome` → on arrival `ReleaseAtBase` calls `status.EndDuty(hour, shiftEnded)` and `pilot.activity=Idle`.
- **Current test gap:** `PilotDutyTests:PilotFatigueUsesActiveLeaseAndStopsAtExhaustion` asserts latch at 0.90 blocks new work but does not advance time during `ReturningHome`.
- **Smallest correction:** Add EditMode test `PilotFatigueAccruesDuringCrewReturn` — assign pilot, `SetGameHour(0)`, `SimulationTick(0)` to board, `AdjustFatigue(0.85)`, `ship.RequestRelease`, `crew.RequestRelease`, tick `TickResponsibleShipDuties(2h)`, assert `fatigue` increased and `IsExhausted` true. No code change.
- **Verification:** That test + inspection of `ShipCrewDutyComponent.SimulationTick:ReturnDestination` path arriving at base.

---

### F-S2 — `CompletingCommittedWork` never produced for facility workers — **Inconsistency — LOW**

- **Severity:** Low (docs-code mismatch). **Consequence:** Future UI that shows “Finishing committed work” for a farmer will never see the state; or a developer may wait for `Completing` on a farmer who will never produce it.
- **Source references:**
  - `Assets/Scripts/ColonyPrototype/People/ColonistStatusComponent.cs:19-28` — `ColonistDutyState { ReleasedResting, ScheduledShift, AcceptingNewWork, CompletingCommittedWork, ReturningHome }` — docstring: “shift/release boundary reached; finish accepted work”
  - `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs:357-389` — `TickColonist` for `Working` sets `AcceptingNewWork`; `RequestDutyRelease` sets `Completing` only for pilots; facility `ReconcileColonist` calls `EndDutyIfActive(..., ShiftEnded/Exhausted)` directly, never `RequestDutyRelease`
  - `Assets/Scripts/ColonyPrototype/Vehicles/ShipCrewDutyComponent.cs:52` — `RequestRelease` correctly sets `Completing` vs `ReturningHome`
- **Analysis:** Facility work is continuous (`ResourceConverterComponent.TickContinuous` with no bounded job), so there is no “already-accepted discrete work” to complete after shift end — `Completing` *should* be ship-only. The ticket wording (“shift/release boundary reached; finish accepted work”) is ambiguous for continuous conversion. Current mapping is actually clearer than the ticket suggests.
- **Smallest correction:** **Do not change code.** Update `STAFFING_ARCHITECTURE.md:27-30` to note: “`CompletingCommittedWork` is produced only for ship `ResponsiblePilot` duties while finishing a committed `Transport`/`Extraction` operation; facility duties transition directly `AcceptingNewWork → ReleasedResting` or `ReturningHome`.” Prevents future UI bug.
- **Verification:** Manual inspection that `StaffingManagerTests:ArrivingAfterShiftEndedDoesNotGrantWork` and `FacilityPerformanceTests` never expect `Completing`.

---

### F-S3 — Midnight recovery interacts with 0.10/h rates — **Product Decision — LOW**

- The `Daily8HourShifts` product explicitly says: A+B gives 16h coverage with 0.65 multiplier = 10.4 equivalent full-output hours, vs A concentrated = 8×1.0 = 8. The third daily window remains uncovered unless C is explicitly assigned. Fatigue 8h work (+0.8) needs 8h sleep (−0.8) — fits within the 16h off-duty complement (or 8h if staggered). No bug. Do not tune thresholds now.

---

## 5. Observability

### What Exists

| Evidence | Durability | Location |
|---|---|---|
| `DutyRecord { workplace/role/shiftId, startGameHour, endGameHour, workedGameHours, fatigueAtStart/Release/End, endReason, releaseRequested, isPilotDuty/ship }` — cap 32 | **Durable** (serialized on `ColonistStatusComponent`) | `ColonistStatusComponent.cs:45` |
| `TransportContract { contractId, demandId, priority, source/destination LocationAnchor, resource, quantity/loaded/delivered, state, assignedVehicle, creationTime }` | **Durable** (serialized list in `ContractManager`, never pruned) | `ContractManager.cs:12`, `TransportContract.cs:20` |
| `FreightDemand { demandId, desiredQuantity, activeSinceGameHour, planningStatus, inboundQuantity }` | **Durable** (but demandIds reset on manager replacement) | `LogisticsManager.cs:12` |
| `ShipComponent { releaseRequested, releaseReason, responsiblePilot, movementOwner }` + `ShipCrewDutyComponent.state/diagnostics` | **Durable** (serialized) | `ShipComponent.cs`, `ShipCrewDutyComponent.cs` |
| `StaffingManager.lastScheduleDump` multiline table + `BuildDailyScheduleTable()` + `[ContextMenu] Dump` | **Durable** (serialized string) | `StaffingManager.cs:104` |
| `SimulationLog.entries` capacity 100, `SimulationTime.FormatTimestamp` prefix | **Rolling** (in-memory, wraps) | `SimulationLog.cs:18` |
| `StaffingComponent.DebugRows { assigned/present/working/activeQualified }` | **Volatile** (recomputed per tick) | `StaffingComponent.cs:37` |

### F-Ov1 — Missing correlation between duty and contract — **Gap — MEDIUM**

- **Severity:** Medium. **Consequence (developer):** After a multi-day run you can say “Pilot A was `AcceptingNewWork` at Day 2 03:00” but cannot answer “which freight `demandId` were they flying for” without correlating timestamps manually across `SimulationLog`.
- **Source:** `ColonistStatusComponent.DutyRecord` stores `workplace/role/shiftId/ship` but not `contractId`/`demandId` for `isPilotDuty` duties. `TransportExecutor.CurrentContract` holds the link but is not persisted into the duty record.
- **Scenario:** Freight demand 7 (Water, target 16, priority 9) is serviced by Pilot A at 17:30. Duty history shows `isPilotDuty=true, ship=Shuttle 1, shift A, worked 1.2h` — no `demandId=7` or `contractId=42` to join to `ContractManager` history.
- **Smallest correction:** Add `int contractId`, `int demandId` to `DutyRecord` (0 when none), populated from `TransportExecutor.CurrentContract` (or `contract.demandId`) when `BeginPilotDuty`/`RequestDutyRelease` is called for the responsible pilot, and cleared on `EndDuty`. One non-breaking serialized field addition.
- **Verification:** New test `PilotDutyRecordsContractCorrelation` — assign pilot, create freight demand, `TryAssignNext` → pilot boards, `TickResponsibleShipDuties` → `ActiveDuty.contractId == executor.CurrentContract.contractId`.

### F-Ov2 — Sleep recovery is invisible — **Gap — LOW**

- **Severity:** Low. **Consequence:** Fatigue dropping from 0.60 → 0 during 6h sleep shows no log line; post-mortem sees start 0.60 and end 0.20 with no intermediate evidence.
- **Source:** `ColonistStatusComponent.ApplySleepRecovery` → `AdjustFatigue` → `RefreshExhaustionLatch` (no log). Logging only on `LogDutyStarted`/`RequestDutyRelease`/`EndDuty`.
- **Scenario:** Exhausted farmer at 0.90 sleeps 7h at `restfulness=1.0` → 0.20, latch clears at tick 7. No log line at the clear moment; developer sees `IsExhausted` flip with no timestamped reason.
- **Smallest correction:** In `ColonistStatusComponent.RefreshExhaustionLatch` (or `ApplySleepRecovery`), when `exhausted` transitions `true → false`, emit one `SimulationLog.Log($"{ColonistLabel()} recovery: fatigue {old:0.00}→{fatigue:0.00} at {SimulationTime.FormatTimestamp(hour)}")`. One line per recovery, not per tick.
- **Verification:** Test `ExhaustionLatchClearIsLogged` — set `fatigue=0.90` (latch true), `ApplySleepRecovery(7h)` → latch false, `SimulationLog.entries` contains “recovery”.

### Rolling Log Noise Assessment

- **Not excessive:** `SetDiagnostic` logs only on change; `BlockReasons` evaluated on demand; `ResourceConverter` exposes `BlockedReason` property without tick logging. Per-tick logging is correctly avoided.
- **Capacity note:** At 1 game-hour/sec, 100-entry `SimulationLog` wraps in ~2 hours of heavy logistics. For a 48-hour smoke run, either increase `capacity` to 300 or add a filtered `FileLogger` (contracts + duty transitions only). Do not log every tick.

### Survives Leaving Play Mode?

`dutyHistory` (32) and `ContractManager.contracts` (history) are MonoBehaviour-serialized lists — they persist in the open scene until you *exit* Play Mode, then the scene reverts (expected Unity behavior). Durable evidence for the slice therefore means capturing `StaffingManager.lastScheduleDump` (multiline) and exporting `SimulationLog.entries` to a file or `EditorPrefs` on `OnDisable`/`OnDestroy`, or simply invoking the existing `[ContextMenu] Dump Daily Work/Off-Duty Schedule` and saving the Console output as the acceptance artifact. Do not serialize the rolling log itself.

---

## 6. Tests and Acceptance

### What Is Actually Executed

| Pass | Claimed | Verified |
|---|---|---|
| Source assembly compile (`dotnet build` 3 projects) | `STAFFING_PATCH_ACCEPTANCE.md`: “Passed: Runtime, Tests, PlayModeTests, 0 errors, 0 warnings” | **Verified** — generated `.csproj` builds after Unity import; log shows `Tundra` builds with `ExitCode 0` after transient dependency cascade |
| Unity Editor import/compile | “`Editor.log` records Runtime/Editor/Tests Csc jobs `ExitCode 0`” | **Verified** (per log excerpt) |
| **EditMode tests** | “Pending licensed Unity run” | **Honest and still pending** — batch `Unity -runTests -testPlatform editmode` blocked by “another instance already has this project open” and earlier by `LicenseClient-artwh refused`. No totals claimed — correct. |
| **PlayMode tests** | “Pending licensed Unity run” | **Honest and still pending** |
| **48-hour `SpaceSim.unity` smoke** | “Pending” + required `SpaceSim.unity` A/B split parity (8×1.0 vs 16×0.65) | **Still pending** |
| **Reassignment / in-flight / provider-toggle / converter-toggle lifecycle checks** | “Pending the explicit checks listed in `09_T07_TRUE_ACCEPTANCE.md`” | **Still pending** |

**Bottom line:** Source is compilation-clean, but the product gate **is not yet met**. Do not claim acceptance until a licensed `batchmode` run records EditMode/PlayMode totals and the smoke run artifact is captured.

### Test Strengths (keep)

- `SimulationTimeTests` — 24h boundaries, negative modulo, `FormatTimestamp` carry, `ManagerExposesCalendarViewWithoutWrappingElapsedHours`.
- `StaffingTests` — multi-class (`Pilot` + `Farm Technician`), skill≠class, proficiency clamp, duplicate/null authoring errors, `ShiftA/B/C` windows, wraparound `Night 20+8`, `HasShift`/`IsShiftActive`, `EmploymentAssignment` shape.
- `FacilityPerformanceTests` — 0 active blocks (`IsOperational false`, multiplier 0), curve `0→0, 1→0.65, 2→1.0, 3→1.2`, clamp beyond curve, identity-independent (removing first worker still leaves 2), optional role (`min 0` does not block), `Doctor`+`Nurse` independent channels, `SkillBonusScalesBaseContribution`, `SkilledWrongClassWorkerContributesNothing`, `MultipleProvidersMultiplySameEffect`, `DisabledAndReenabledProviderChangesNextEvaluation`, `AutomatedFacilityWithoutProvidersIsOperationalAtOne`.
- `StaffingManagerTests` — wrong class/shift/role-offer/capacity rejections, `OffShiftWorkerRests` vs `OnDutyWaitsAndRequestsCommute`, `OnDutyAtWorkplaceBecomesWorking`, `TwoWorkersSameRouteAreGroupedIntoOneContract`, `OppositeShiftsUseDifferentCommuteWindows` (immutable first flight), `FatigueAtNinetyPercentStopsWorkImmediately`, `DailyScheduleTableIncludes...`.
- `PilotDutyTests` — single shuttle shared across A/B/C (including explicit C boarding 16-24), `SameShiftCapacityIsOneButOtherShiftAvailable`, `AtBasePilotBoardsDirectlyWithoutSelfCommute`, `DifferentHomeCannotJoinExistingCrewBase`, `PilotWithExistingPassengerTripCannotBoardUntilTripCompletes`, `DailyPilotShiftDoesNotRestartAtSixteenHours`, `ScheduleTableIncludesPilotDailyOffDutyWindow`, `PilotFatigueUsesActiveLease...`, `ReleaseStillReturnsPilotWhenShipIsOutOfService`, `UnassignKeepsResponsiblePilotUntilSafeRelease`.
- `ResourceConverterTests` — fractional continuous (`0.4h → 0.2 progress`), discrete batch withheld until complete, `CompletedBatchWaitsWithoutLosingOutput`, `FacilityPerformanceGatesAndScalesConversion`, `ConverterWithoutPerformanceRunsAtFullRate`, `ContinuousDiscreteRecipeIsRefused`.
- `InventoryComponentTests` — capacity, `Reserve`/`Available`/`WithdrawReserved`, `ReleaseReservation`, discrete fractional rejection.
- `UnifiedDispatchTests` — `Priority10PassengerBeats9Freight`, `Priority10FreightBeats9Passenger`, `EqualPriorityPreferFreight/Personnel`, `PreferenceDoesNotOverridePriority`, `DispositionHardFiltersCategory`, `ForegroundBeatsBackgroundRegardlessOfPriority`, `BackgroundFillUsesIdleVehicle...`, `OldestEqualCandidateWins`, `DiscreteFreightIsWholeUnits`, `FreightReservationWaitsUntilVehicleWins`, `ChangingPriorityAffectsNextAssignmentOnly`.
- `RuntimeLifecyclePlayModeTests` — `ComponentCreatedBefore/AfterManagerIsDiscoveredAndManagerReplacementResumes`, `TickPriorityRunsLowerNumbersFirst`, `SimulationClockCrossesMultipleMidnightsWithoutResetting`.

### F-T1 — Tests that manufacture unrealistic state hide integration gaps — **Risk — MEDIUM**

- **Source:** `Assets/Tests/EditMode/FacilityPerformanceTests.cs:ActiveWorker` — directly sets `colonist.currentEmployment = new EmploymentAssignment(workplace, role, shiftId)`, `colonist.activity = Working`, `colonist.currentLocation = workplaceLocation`, then `manager.RegisterColonist(colonist)`. This bypasses `StaffingManager.Assign` validation (`OffersRole`, `HasClass`, `IsShiftActive`, `exertionMultiplier`/`recoveredThreshold` checks) and the exhaustion latch.
- **Consequence:** A regression that breaks `OffersRole` or `HasClass` in the performance path still passes — the test forced the state. The test proves *curve aggregation* isolation correctly, but not the *wiring* from validated employment to operational blocking.
- **Smallest correction (keep isolation, add one thin integration test):**
  ```csharp
  [Test] public void FacilityOperationalViaStaffingManagerTick() {
      var farmer = harness.Colonist("F", home, farmClass);
      Assert.That(manager.Assign(farmer, farm, farmOperator, "A"), Is.EqualTo(Applied));
      farmer.currentLocation = farm.workplaceLocation;
      harness.SetGameHour(clock, 1f); // Shift A
      manager.SimulationTick(0f);
      Assert.That(performance.IsOperational, Is.True);
  }
  ```
  This single test covers the wiring without duplicating all curves.
- **Verification:** That test fails if `ValidateAssignment` or `IsShiftActive` regresses, while existing `FacilityPerformanceTests` remain pure.

### F-T2 — Over-specified formatting — **Brittle — LOW**

- `StaffingManagerTests:DailyScheduleTableIncludesAssignedAndUnassignedColonists` asserts `Does.Contain("00:00-08:00")`. Correct for protecting the observational contract, but will churn if `FormatWorkWindow` changes. **Keep** — it guards against silent table regression.

### Recommended Additional Tests (only behavior-protecting)

1. `DisabledWorkerStillCountsTowardCapacity` (covers F-O1)
2. `PilotFatigueAccruesDuringCrewReturn` (covers F-S1)
3. `ContractCancelledWhenPassengersDestroyedMidFlight` (covers F-O2)
4. `FacilityOperationalViaStaffingManagerTick` (covers F-T1)
5. Existing `ThreeDailyShiftsCanProvideFullDayCoverage` already covers full-day; keep.

Do **not** add tick-spam logging tests or exhaustive curve permutations. Tests must serve development; development must serve the game.

---

## 7. Fit for the Small Playable Slice

### Milestone Intent (from prompt)

> “deliberately small, playable slice. We want to deepen the existing colony, farm, water-processing, mining, and shuttle loop before adding more economic breadth.” Next work: colony-wide management UI; individual facility/ship/colonist inspection & management; ship flight models, docking ports, docking/undocking maneuvers; visible inventory (boxes on racks); placeholder ships/stations with coherent scale/placement; physical colonist transitions (habitat beds → shuttle seats → workplaces).

### Ready Paths

| Slice Need | Why It’s Ready | Evidence |
|---|---|---|
| **Selection** | Raycast → `GetComponents<StaffingComponent/FacilityPerformanceComponent/InventoryComponent/ColonistAgent/ShipComponent>` — no scanner | `SpaceSim.unity` already composes Command Post, Farm 1, Water Processor, Shuttle 1, Mining Ship generically |
| **Commands** | All validated — `StaffingManager.Assign` returns `AssignmentResult`, `ResourceConverter.SelectRecipe` validates `Validate`/`IsValid` | `StaffingManager.cs:188`, `RecipeDefinition.cs:18`, `ResourceConverterComponent.cs:46` |
| **Simulation speed/pause** | `SimulationManager.paused`, `speedMultiplier`, `gameHoursPerRealSecond` decoupled from rates (which use `deltaGameHours`) | `SimulationManager.cs:31`, `ColonistStatusComponent:ApplyWorkFatigue(delta*rate)` |
| **Placeholder scale** | Pure `transform` scale, no simulation dependency | `SpaceSim.unity` anchors at coherent positions |
| **Deepen existing loop** | Ingredients present: `PopulationResourceConsumer` (habitation consumption), `ResourceConverter` (Farm/Water Processor), `ResourceStockPolicy` (import/export), `LogisticsManager` unified arbitration, `ExtractionMissionController` + `ResourceCollector` + `ResourceDeposit` (mining) | All content Inspector-editable per `CONTENT_AUTHORING.md` + `STAFFING_AUTHORING.md` — no C# for new resources/recipes/shifts/roles |

### F-F1 — `SelectRecipe` seam allows direct field poke — **LOW**

- **Source:** `Assets/Scripts/ColonyPrototype/Production/ResourceConverterComponent.cs:14` — `public RecipeDefinition activeRecipe` is public mutable; UI could assign directly, bypassing `Validate`/`IsValid` and `ValidateAmounts` (fractional discrete guard).
- **Smallest correction:** Make backing field `private [SerializeField] RecipeDefinition activeRecipe` with public getter `ActiveRecipe`, require `SelectRecipe` for mutation, add `OnValidate` guard that clears invalid assignment and logs `Debug.LogError`. One-line API tightening.
- **Verification:** EditMode test assigns `activeRecipe` directly via reflection in old revision → now requires `SelectRecipe` and invalid discrete continuous is rejected.

### F-F2 — Save/Load — **Deliberately out of scope for slice**

- No save system exists. StableIds exist on every definition (`stableId` on `ResourceDefinition`, `WorkerClassDefinition`, `SkillDefinition`, `FacilityEffectDefinition`, `ShiftPatternDefinition`, `StaffingRoleDefinition`, `RecipeDefinition`) but scene references are Unity object refs. For the slice, no save is required. When needed, replace direct `ResourceDefinition` refs with `stableId` lookups at save time (migration already noted in `tickets/24_Hour_Day_Clock_Patch/FILE_MANIFEST.md`). Do not add JSON now.

### F-F3 — Spatial placeholder — **Safe to defer**

- No `HackScale` ScriptableObject needed now. Normalize all `SpaceSim.unity` anchors to a 10 m grid, note the grid in a comment on `LocationAnchor`, and scale meshes with `transform.localScale` only.

### What Is *Not* Needed Before the Slice

See §12.

---

## Dependency-Ordered Repairs Before the Slice

Do these in order. Each is a few-hour change and each is verifiable in a headless-dotnet + licensed-editor run. Nothing here demands a framework rewrite.

| # | Repair | Primary Finding | Effort | Verification Gate |
|---|---|---|---|---|
| **1** | **Fix disabled-worker capacity** — change `StaffingManager.CountAssigned` + `CollectAssignedWorkers` to count employed regardless of `isActiveAndEnabled` (or keep disabled-employed shadow set) | F-O1 | 1 h | `DisabledWorkerStillCountsTowardCapacity` EditMode test — assign 3, disable 1, 4th → `RejectedAtCapacity`, re-enable still 3 |
| **2** | **Prune passenger contracts on destroy** — `PassengerCarrierComponent` skip/parent nulls, `ContractManager.HasActivePassengerFor*` skip null, `TransportExecutor` cancel when every passenger null/destroyed | F-O2 | 2 h | `ContractCancelledWhenPassengersDestroyedMidFlight` — `DestroyImmediate` one of two passengers mid-flight → contract cancelled, log “all passengers lost”, new contract assignable |
| **3** | **Add inventory `OnChanged` event** — `InventoryComponent` fires `OnInventoryChanged(resource, old, new)` once per successful mutation after `Refresh()` | F-P1 | 1 h | Rack view subscribes `OnEnable`/unsubscribes `OnDisable`; EditMode test: `Add(Water,5)` → one event delta +5; discrete fractional rejected → no event |
| **4** | **Harden observability** — (a) add `contractId`/`demandId` to `DutyRecord` for pilots, (b) log one line when exhaustion latch clears, (c) ensure `StaffingManager.lastScheduleDump` is captured as file artifact on `OnDisable` or via `[ContextMenu]` | F-Ov1, F-Ov2 | 2 h | `PilotDutyRecordsContractCorrelation` test; `ExhaustionLatchClearIsLogged` test; 48h smoke run artifact contains `lastScheduleDump` + `SimulationLog.entries` file |
| **5** | **Run the pending gates** — licensed `Unity -batchmode -runTests -testPlatform editmode/playmode`, record totals in `STAFFING_PATCH_ACCEPTANCE.md`, and run a 48-game-hour `SpaceSim.unity` smoke at accelerated `gameHoursPerRealSecond` capturing `lastScheduleDump` + `SimulationLog` to file | §6 | 2 h (wall) | EditMode/PlayMode totals replace “Pending” in `STAFFING_PATCH_ACCEPTANCE.md`; smoke log proves A at 0h/24h is same window and `WorkScheduled` vs `ReturningHome` transitions |
| **6** | **Document `CompletingCommittedWork` as ship-only** — update `STAFFING_ARCHITECTURE.md:27-30` + add one thin integration test | F-S2, F-T1 | 0.5 h | Doc says “Completing only for `ResponsiblePilot` finishing committed `Transport`/`Extraction`”; `FacilityOperationalViaStaffingManagerTick` passes |
| **7** | **Encapsulate `ResourceConverter.activeRecipe`** — private `[SerializeField]` backing field + `SelectRecipe` + `OnValidate` guard | F-F1 | 0.5 h | Direct assignment of invalid discrete continuous recipe is rejected; `SelectRecipe` still the only valid path |

**Exit criteria for “ready for slice”:** #1-3 merged, #4 merged, #5 licensed totals + smoke artifact captured, #6-7 merged. Total < 2 days of focused work.

---

## Safe to Address While Building the Slice

These are natural extensions of the seam work and do not block the slice start:

- **Docking:** Add `IDockingSurface` interface + `DockingPortComponent` skeleton (proxy behind `ShipComponent.CurrentDock` — F-O3). Ship flight models can implement `MoveToward` with approach vectors without touching `MovementOwner` logic.
- **Colonist visuals:** Add `ColonistVisualMover` (lerps `transform.position` toward `currentLocation.transform.position` at ~0.2 game-hours, visual only — F-P3). Keep `currentLocation` immediate.
- **Rack visuals:** Implement `RackInventoryView` subscribing to `InventoryComponent.OnChanged` (never writes inventory). Spawn pool of box GOs per discrete unit, interpolate fractional.
- **Log capacity:** Bump `SimulationLog.capacity` 100 → 300 or add `FileLogger` (contracts + duty transitions only). Do not log every tick.
- **Tests while building:** Add the 4 risky-integration tests (F-T1, F-S1, capacity, passenger destroy) alongside the feature they protect — tests serve development.

---

## Deliberately Leave Alone

**Do not turn this into a speculative framework rewrite.** The following are explicitly out of scope and *correctly* excluded per the locked design contracts and the “small playable slice” milestone:

- **Automatic replacement staffing / call-ins / vacancy filler** — “No staffing UI, automatic vacancy filler/call-in allocator … are part of this patch” (`STAFFING_PATCH_ACCEPTANCE.md:Excluded features`). Unstaffed shifts intentionally mean downtime. Do not add.
- **Contract rerouting / multi-stop transport / predictive contract acceptance / early commuting** — Deferred to future work (prompt). Current flights complete before new destination is reconciled — keep.
- **Hunger / thirst / morale / health gameplay** — Not in this patch. `PopulationResourceConsumer` intentionally does not create personal inventories.
- **General overtime scheduling / 8-on/8-off rotation** — “Future 8-on/8-off rotation is out of scope … must be a separate rotation/schedule policy” (`01_LOCKED_DESIGN.md §7`). Do not restore `ShiftPatternDefinition.cycleHours`.
- **Universal `WorkOrder` / action abstraction over `TransportDispatchCandidate`** — `TransportDispatchCandidate` is *deliberately narrower* than a universal abstraction (`LogisticsManager` header comment). Do not generalize logistics now.
- **`ShiftPattern.cycleHours` resurrection** — Correctly removed.
- **Tick priorities** — 100/200/300/400/1000 is correct and ensures fatigue before output.
- **MonoBehaviour-network vs pure-C# backend** — The network *is* the requirement (“Being able to add, disable, adjust, and re-enable individual components while experimenting in a running scene is a design requirement”). Do not “fix” it away.
- **More abstractions = more readiness** — Do not reward architectural complexity. The current 42 runtime scripts + content ScriptableObjects are the right shape for the slice.

---

## Remaining Unknowns Requiring Running Unity / Unavailable Artifacts

These cannot be verified from source inspection alone and require the licensed editor + live `SpaceSim.unity` scene:

1. **Actual 48-hour game-hour advancement under real `tickIntervalSeconds=0.1`, `gameHoursPerRealSecond=1`, `speedMultiplier=1` vs accelerated 10k** — Do A/B farmers still produce exactly 8×1.0 full-output hours per 16h cycle and A/B split still 16×0.65 = 10.4 equivalent hours when `ResourceStockPolicy` commutes and `LogisticsManager` arbitration latency are included? Requires live run with `SpaceSim.unity` shuttles at `movementSpeed 12` and 10 m anchors.
2. **A at 0h vs 24h is same window (not a second trigger at 16h)** — needs the `PilotDutyTests:DailyPilotShiftDoesNotRestartAtSixteenHours` logic exercised live with `SimulationManager` advancing through two midnights (the PlayMode test `SimulationClockCrossesMultipleMidnightsWithoutResetting` proves the clock but not the staffing reconciliation in the built scene).
3. **Visual placement coherence of Farm/Water Processor/Shuttle/Mining Ship after adding real meshes** — need editor viewport check; current scene uses cubes + `Person.prefab` capsule, no scale reference asset.
4. **Licensing-gated test totals and the Tundra rebuild transient** — the repo log shows transient “test-assembly dependency cascade while importing”, then 3 successful Tundra builds with `ExitCode 0`. A clean ` -batchmode -nographics -quit -runTests` in a licensed editor with no other instance open is still required to replace “Pending” with true totals.
5. **`Person.prefab` (`ColonistAgent` + `ColonistStatusComponent` + capsule) survival under “Enter Play Mode without Domain Reload”** — requires toggling the editor setting and running `RuntimeLifecyclePlayModeTests` under that mode.
6. **`Library/` + `ProjectSettings/` + `UserSettings/` not inspected** — asset import settings, physics, time scale, and package lock may affect `MovementComponent.ArrivalEpsilon` (0.05) and `ShipMovementComponent` vs `NavMesh`.

---

## Source Index (Reviewed Revision)

**Commit:** `15fb66ba52ea210d57f593acc4ce4d70ce618a31`  
**Runtime scripts (42):** `Assets/Scripts/ColonyPrototype/Content/*`, `Core/*`, `Economy/*`, `Extraction/*`, `Logistics/*`, `People/*`, `Production/*`, `Vehicles/*`, `World/*`  
**Scene/Prefab:** `Assets/SpaceSim.unity`, `Assets/Person.prefab`, `Assets/GameData/**/*` (`Daily8HourShifts.asset`, `FarmOperator.asset`, `Pilot.asset`, `Food/Water/Ice` resources, recipes)  
**Tests:** `Assets/Tests/EditMode/*.cs` (21 files), `Assets/Tests/PlayMode/RuntimeLifecyclePlayModeTests.cs`  
**Documentation:** `ARCHITECTURE.md`, `STAFFING_ARCHITECTURE.md`, `STAFFING_AUTHORING.md`, `STAFFING_BASELINE.md`, `STAFFING_PATCH_BASELINE.md`, `STAFFING_PATCH_ACCEPTANCE.md`, `CONTENT_AUTHORING.md`, `tickets/24_Hour_Day_Clock_Patch/**`, `tickets/Staffing_Fatigue_Immediate_Assignment_Patch/**`

All findings above reference these exact files at the reviewed revision. History before `15fb66b` (e.g., `FarmController`, `WorkScheduleComponent`, `RecipeStaffingRule`) is distinguished as superseded and not evaluated as current behavior.

---

*Generated by adversarial review per the “small enjoyable game” brief. Skeptical, specific, and scoped to minimum resilient repairs. No speculative framework, no demand for a different engine architecture, no equation of more tests with readiness.*
