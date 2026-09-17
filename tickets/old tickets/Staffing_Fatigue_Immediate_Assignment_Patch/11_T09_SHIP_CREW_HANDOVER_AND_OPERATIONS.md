# T09 — Ship crew handover and operations

Status: implemented; Unity PlayMode/lifecycle validation remains pending because
the project is open in another editor instance.

## Outcome

At most one eligible on-shift pilot controls a shuttle. Shift end, exhaustion, reassignment, or unassignment stops new dispatch, lets accepted work finish, returns the shuttle to its crew-change base, and releases the cockpit. Another assigned pilot can then board for their own active shift. The outgoing pilot has no claim on the ship while resting.

## Selection and boarding

`ShipCrewDutyComponent` owns the responsible-pilot lease and physical crew lifecycle. On each enabled simulation tick while the ship is safely docked at `crewChangeBase` and has no responsible pilot:

1. Query assignments for `operatingRole` whose named shift is active at the current game hour.
2. Keep only colonists who still share the base as home, have the required class, are not exhaustion-latched, are physically at the base, have no active passenger trip, and are not physically responsible for another ship.
3. If exactly one eligible pilot exists, board them from the base to `ShipLocation`, begin ship duty, and set `OnDutyCrew`.
4. If none exists, remain parked and expose the most specific stable blocker.
5. If more than one exists because authored shift windows overlap, remain parked with an `ambiguous active pilot coverage` diagnostic. Do not choose by list order.

An assigned pilot away from the shared base requests ordinary passenger transport to the base at priority 10 only while their shift is active. Do not target the moving ship anchor. A pilot already at the base boards directly without a passenger contract. Do not create automatic call-ins for empty shifts or use off-shift assigned pilots as substitutes.

## Responsible-pilot lease

`ShipCrewDutyComponent.ResponsiblePilot` is serialized runtime state and is the sole source for:

- operational crew readiness;
- passenger-manifest self-exclusion;
- fatigue/duty accounting;
- safe completion after employment changes;
- diagnostics showing who currently controls the ship.

`ShipComponent.IsOperationallyCrewed` requires an enabled operational ship, a valid ship anchor, a responsible pilot physically aboard, valid qualification, an active duty record for this ship, and no release request. Employment is checked when granting a lease; ongoing accepted work relies on the lease and duty record so immediate reassignment cannot strand a flight.

## Release state machine

Request release once, preserving the first reason and request time. Reasons are ShiftEnded, Exhausted, Reassigned, Unassigned, and WorkplaceUnavailable.

1. The instant the responsible pilot's shift ends, fatigue reaches 0.90, employment changes, home/base validity fails, or required crew configuration becomes unavailable, set release requested. The ship becomes unavailable for new transport or extraction work in the same tick.
2. If a transport contract or extraction mission has already been accepted, finish its existing pickup/travel/unload sequence. Loaded trips remain immutable.
3. After the accepted operation is completely idle, travel crew-only to `crewChangeBase`. This movement creates no passenger contract and carries the responsible pilot aboard.
4. Continue charging Pilot-role work fatigue during standby, accepted work, and the return flight, clamped at 1.0. Do not charge it from movement or executor code.
5. At the base, disembark the responsible pilot to the base anchor, end the exact duty record with the preserved release reason and actual end time, clear the physical lease and release state, and leave the shuttle parked.
6. Normal staffing reconciliation then makes the colonist sleep if they are still based there and otherwise routes them according to their current employment. The former pilot cannot operate or reserve this shuttle merely because they remain assigned to a later shift.

If the ship is already at base, release and disembark without flight. If the base or movement component is missing, hold position, decline new work, preserve the lease/cargo/passengers, and expose a specific diagnostic. Never teleport or invent a destination.

## Operation and movement ownership

Maintain one explicit movement owner per ship: None, Transport, Extraction, or CrewReturn. Only the owning enabled controller may advance movement in a tick.

- Transport and extraction may start only when `IsOperationallyCrewed`, movement owner is None, and release is not requested.
- An accepted operation may continue after release is requested.
- CrewReturn may claim ownership only after transport/extraction is fully idle.
- Ownership transfers and contract completion must be ordered so `TryAssignNext()` cannot acquire new work between operation completion and crew return.
- Disabling the current owner pauses retained state. Re-enabling resumes it. No other controller may steal ownership while it is disabled.
- Manager/component recreation must recover serialized ownership and contract/mission state without duplicating or abandoning work.

## Passenger and dispatch reconciliation

Use `ResponsiblePilot`, not an assigned-crew query, when rejecting a ship's own pilot from its passenger manifest. Assigned pilots may legally ride another ship.

Keep the shared, side-effect-free boarding check in dispatch and actual loading. A carrier-specific impossible unboarded pickup is rejected/cancelled with a reason so another valid contract may run. A loaded flight still completes. Temporary disablement pauses and never cancels.

## Fatigue and shift details

- Work fatigue remains 0.10 per game hour times `operatingRole.exertionMultiplier`.
- Sleep recovery remains 0.10 per game hour times habitat restfulness and occurs only during actual sleep at home.
- Shift windows are fixed. Late arrival does not grant a new eight-hour interval.
- A pilot whose shift begins while the prior pilot is returning waits at the base.
- A pilot whose shift expires while waiting simply remains off duty.
- There is no automatic overtime replacement, forced-work toggle, or least-fatigued call-in in this patch.

## Implementation targets

- `Assets/Scripts/ColonyPrototype/Vehicles/ShipCrewDutyComponent.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/ShipComponent.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/TransportVehicleComponent.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/TransportExecutorComponent.cs`
- `Assets/Scripts/ColonyPrototype/Extraction/ExtractionMissionController.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/PassengerCarrierComponent.cs`
- `Assets/Scripts/ColonyPrototype/Logistics/ContractManager.cs`
- `Assets/Scripts/ColonyPrototype/Logistics/LogisticsManager.cs`
- `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs`
- `Assets/Scripts/ColonyPrototype/People/ColonistStatusComponent.cs`

## Essential focused tests

Use public behavior, real simulation ticks, real movement, and real contract/mission completion. Do not assign private states or mine contract history to fake proof.

- A at-base on-shift pilot boards; assigned off-shift B remains at home. At handover, A disembarks and sleeps, B boards the same shuttle, and both employment records remain unchanged.
- Shift end and exhaustion during an accepted passenger/freight trip each prevent another dispatch, finish unload, return crew-only to base, disembark, and preserve the correct request/end times and reason.
- Release during extraction finishes extraction and unload, returns to base, and never overlaps movement owners.
- Reassignment during flight changes employment immediately; the old duty lease completes the accepted work and base return, then clears without restarting old employment.
- Missing relief leaves the shuttle parked after release. No off-shift pilot boards and no ship is reserved by the sleeping pilot.
- Overlapping authored active pilot shifts block boarding with an ambiguity diagnostic.
- Disable/re-enable transport, extraction, crew duty, ship, and simulation manager at representative points; each resumes without teleport, duplicate fatigue, duplicate contract, or lost lease.
- The responsible pilot cannot be a passenger on their own ship; another assigned/off-duty pilot may ride it, and a pilot may ride another ship.

Run Unity compilation, the focused EditMode tests, and lifecycle PlayMode tests before T10.
