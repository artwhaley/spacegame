# T03 — Staffing State Machine, Fatigue, and Transport Reconciliation

Depends on T02.

## Goal

Make staffing apply fatigue, shifts, duties, and commute requests according to the locked state machine.

## Required production changes

### Status resolution

`StaffingManager` must obtain the colonist's `ColonistStatusComponent`. All authored/person-prefab colonists must have one after T06. For a runtime-created colonist without one, add it on registration so status is never silently absent.

### Tick sequence

For each known enabled colonist, every staffing tick performs:

1. Determine whether the elapsed interval was Working or Sleeping.
2. If Working, add worked time to the active duty and apply role-adjusted fatigue.
3. If Sleeping, apply home-habitation-adjusted recovery.
4. If the fatigue change sets/clears exhaustion, retain the latch result.
5. If an active passenger contract covers the colonist, leave activity as Passenger and make no route request.
6. Otherwise reconcile employment, shift, fatigue, and current location using the exact table in `01_LOCKED_DESIGN.md`.
7. Flush deduplicated commute groups.

When work reaches exhaustion, close duty as `Exhausted` and change away from Working before facility production evaluates for that same tick.

### Duty transitions

- Entering Working calls `BeginDuty`.
- Remaining Working does not create another duty.
- Shift end closes as `ShiftEnded`.
- Exhaustion closes as `Exhausted`.
- Invalid/disabled/destroyed workplace closes as `WorkplaceUnavailable` and routes home.
- T02 assignment APIs close `Reassigned`/`Unassigned` before reconciliation.

### Transport rules

- Never mutate, cancel, retarget, or append stops to an existing passenger contract.
- Reassignment while Passenger changes employment but not the flight or `Passenger` activity.
- After unload, route from the actual landing location to current job if on shift and fit; otherwise route home.
- Deduplicate by colonist: at most one open/assigned/in-progress passenger contract may cover a colonist.
- A commute group contains only colonists sharing source, destination, and priority.
- Do not create source-equals-destination contracts.
- If no vehicle exists, keep `WaitingForTransport` and leave one open contract; do not emit a new duplicate each tick.

### Third-location behavior

Delete diagnostic-only third-location handling. A third location is a routable state, not an error. Apply the locked routing table.

## Essential tests

### EditMode domain tests

- shift boundary math remains correct;
- only Working raises fatigue and only Sleeping lowers it;
- status transitions select the required destination for all home/work/third-location, shift, and exhaustion combinations;
- duty records close with the correct reason.

### PlayMode integration tests

1. A worker at the workplace accumulates `0.10` per game hour and production sees them active below threshold.
2. Crossing `0.90` ends duty and queues home transport in the same tick; the old workplace no longer counts them.
3. At home, the exhausted worker sleeps to `0.20`, then may commute if their shift is still active.
4. Off-shift sleep recovers fatigue; Passenger, waiting, and resting-at-zero do not.
5. A reassigned Working colonist stops old work and requests the new workplace immediately.
6. A reassigned Passenger finishes the original flight unchanged, unloads, then requests a second trip from the landing location.
7. An unassigned Passenger likewise finishes the flight, then requests home.
8. A worker landed after their shift ended routes home and never starts an expired duty.
9. Opposite-shift proof queries assignment/activity/duty records, never passenger-contract history.
10. Repeated ticks create no duplicate contracts.

Use real `ContractManager`, logistics, and transport execution in flight tests. Do not simulate completion by editing contract state directly.

## Acceptance gate

- Focused EditMode and PlayMode tests pass.
- Fatigue threshold affects production in the same simulation tick.
- Existing flights always complete; subsequent routing reflects current assignment.
- Third locations self-reconcile without recurring error logs.
- Explicit shift assignments are never changed by the simulation.
