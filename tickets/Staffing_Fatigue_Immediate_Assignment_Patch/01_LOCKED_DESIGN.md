# Locked Design Contract

This file resolves implementation choices for the entire packet. The executor must not substitute a different design without stopping and obtaining a new product decision.

## 1. Employment remains explicit

Employment is exactly one current `EmploymentAssignment` containing:

```text
workplace + staffing role + shift id
```

The simulation never selects a replacement worker and never rewrites a shift assignment. An uncovered shift remains uncovered. A future player option may automate call-ins, but it is not part of this patch.

Remove `pendingEmployment`, `hasPendingEmploymentChange`, and `AssignmentResult.Pending`. Do not retain obsolete serialized compatibility fields.

## 2. Assignment is immediate and atomic

`StaffingManager.Assign(...)` validates the complete proposed assignment before mutation. On failure, the old assignment and activity remain unchanged. On success, `currentEmployment` changes immediately from any location or activity.

The old workplace stops counting the colonist immediately. If the colonist was Working, close the active duty record with `Reassigned`, remove their production contribution, and reconcile travel immediately.

`Unassign(...)` immediately clears employment, closes active duty with `Unassigned`, removes contribution, and reconciles travel home.

If the colonist is aboard a vehicle or covered by an active passenger contract, do not edit that contract and do not overwrite `Passenger`. Finish the trip. After unloading, the next staffing reconciliation starts a new trip from the landing location if needed.

## 3. Personal status owns fatigue

Add `ColonistStatusComponent` beside `ColonistAgent`. It owns fatigue and duty history; `StaffingManager` decides when work or sleep is occurring and calls its public domain methods.

Serialized defaults:

```text
fatigue = 0.00
workFatiguePerHour = 0.10
sleepRecoveryPerHour = 0.10
exhaustionThreshold = 0.90
recoveredThreshold = 0.20
isExhausted = false
maximumDutyRecords = 32
```

Clamp fatigue and both thresholds to `0..1`. Require `recoveredThreshold < exhaustionThreshold`. Clamp rates and multipliers to non-negative finite values.

Effective work change:

```text
+deltaGameHours * workFatiguePerHour * role.exertionMultiplier
```

Effective sleep change:

```text
-deltaGameHours * sleepRecoveryPerHour * habitation.restfulnessMultiplier
```

Use `1.0` when the role or habitation component is absent. Add `exertionMultiplier = 1.0` to `StaffingRoleDefinition`; add `restfulnessMultiplier = 1.0` to `HabitationComponent`.

Set the exhaustion latch when fatigue reaches or exceeds `0.90`. Keep it set while fatigue decreases through values above `0.20`; clear it only at or below `0.20`. This hysteresis is mandatory and prevents commute oscillation.

Expose one general-purpose `AdjustFatigue(float delta)` method for future effects, plus intent-revealing work/sleep methods. All routes clamp and update the latch consistently.

## 4. Activity and fatigue state machine

Add `ColonistActivity.Sleeping`. Fatigue changes only as follows:

- `Working`: increases at the role-adjusted rate.
- `Sleeping`: decreases at the habitation-adjusted rate.
- every other activity, including Passenger, WaitingForTransport, Idle, Resting, and OnDutyCrew: no change.

At each staffing tick, apply the elapsed interval to the activity that occupied that interval, then reconcile the next activity. If work accumulation reaches the threshold, clamp to at least the exact threshold, close duty with `Exhausted`, stop contribution in the same tick, and queue home transport.

At home:

- exhausted colonist: Sleeping until the recovery latch clears;
- off-shift colonist with fatigue above zero: Sleeping;
- off-shift colonist at zero fatigue: Resting;
- on-shift, non-exhausted, employed colonist: travel to work;
- unassigned colonist with fatigue above zero: Sleeping;
- unassigned colonist at zero fatigue: Resting.

At the assigned workplace:

- on shift and not exhausted: Working;
- off shift or exhausted: request home.

At any third location and not in transit:

- on shift, employed, and not exhausted: request the assigned workplace;
- otherwise: request home.

If source and destination are the same, do not create a contract.

## 5. Duty history is authoritative

Add serializable `DutyRecord` data owned by `ColonistStatusComponent`:

```text
workplace
role
shiftId
startGameHour
endGameHour
workedGameHours
endReason
```

Use `DutyEndReason` values `ShiftEnded`, `Exhausted`, `Reassigned`, `Unassigned`, and `WorkplaceUnavailable`. Only time actually spent in `Working` increments `workedGameHours`. Keep the newest 32 completed records and expose a read-only list. Track the active duty separately and ensure repeated reconciliation does not create duplicate records.

Tests and future UI query duty state/history directly. Passenger contracts are transport facts, not work history.

## 6. Facility providers are live

`FacilityPerformanceComponent` must rebuild its provider set at every `EvaluateAt(...)`. Discover sibling `MonoBehaviour` providers plus `additionalProviders`, deduplicate by object identity, and include only non-null, active-and-enabled behaviours implementing the performance-provider interface.

No permanent `cached` flag. A provider added at runtime participates on the next evaluation. Disable removes its effects; re-enable restores them. Destroy removes it. A disabled `ResourceConverterComponent` retains batch progress and resumes that same batch after re-enable.

## 7. Simulation registration is order-independent

Create a static desired-tickable registry owned by `SimulationManager` APIs:

```text
SimulationManager.RegisterTickable(ISimulationTickable)
SimulationManager.UnregisterTickable(ISimulationTickable)
```

Components call these from `OnEnable` and `OnDisable`; `OnDestroy` may defensively unregister. Registration works before a manager instance exists. A newly created manager adopts enabled desired tickables. A replacement manager does the same. Remove destroyed Unity objects during reconciliation.

Tick iteration is mutation-safe. Additions during a tick start next tick; disabled/removed behaviours are skipped immediately and removed after iteration. Duplicates are impossible.

Add optional `ISimulationTickPriority` with `int SimulationTickPriority`. The manager sorts by priority, then stable registration ordinal. Required priorities:

```text
100 StaffingManager
200 StaffingComponent and production/consumption tickables
300 stock policy and LogisticsManager
400 transport executors and mission controllers
1000 default for any unclassified tickable
```

This ensures fatigue/activity reconciliation occurs before facility output for the same tick.

## 8. Other runtime registries

Apply the same lifecycle outcome, without forcing the exact static-registry implementation, to:

- `PopulationManager` / `ColonistAgent`;
- `LogisticsManager` / `TransportVehicleComponent`.

Enabled members must be known regardless of creation order; disabled members must be absent; re-enabled members must return once; duplicates and destroyed references must be removed. Manager-side discovery on manager creation is acceptable for these two registries. Per-frame or per-tick global scene scans are not.

## 9. Tests serve behavior

EditMode tests cover deterministic domain logic and explicit component APIs. PlayMode tests cover Unity messages, activation, time progression, transport completion, and manager/component creation order. Do not manually invoke private `Awake`, `Start`, `OnEnable`, or `Update`. Do not change production initialization to accommodate EditMode message timing.

Create real Doctor and Nurse class assets. Every role, including test roles, has a class. `StaffingRoleDefinition.Validate` rejects null `requiredClass`.

## 10. Ship crew refinement (T08–T10)

The later ship-crew tickets refine the generic worker rules above without changing facility workers:

- A crewed ship is a normal staffed workplace with explicit role and shift assignments. Employment persists while its crew are off duty.
- A separate responsible-pilot lease represents the one colonist physically controlling a shuttle. Off-shift and sleeping pilots do not retain that lease or reserve the ship.
- Every colonist assigned to a given ship must have the ship's fixed crew-change base as their home habitat. The first successful crew assignment may establish an unset base; later assignments must match it.
- Shift end, exhaustion, reassignment, or unassignment immediately removes readiness for new work. A responsible pilot finishes already accepted ship work, returns the ship to the crew-change base, disembarks, and only then closes physical duty with the preserved reason. Fatigue continues during that safe completion and return.
- The next eligible on-shift assigned pilot may take the responsible-pilot lease only after being physically present at the base and boarding. Empty shifts leave the ship parked.
- A still-unboarded passenger pickup that becomes permanently impossible for its assigned carrier may be cancelled with a recorded reason. Loaded flights remain immutable and complete their destination.

These rules supersede the generic immediate-duty-close and immediate-home-routing language only while a colonist is physically responsible for a ship. They do not create automatic staffing or call-ins.
