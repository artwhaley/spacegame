# T04 — Explicit Ship Travel Phase, Docking Port Seam, and Fail-Closed Movement

## Goal

Make the ship movement seam safe for future flight models and docking maneuvers.

## Changes

1. Replace/augment the ambiguous traveling boolean with an explicit phase:
   - `Docked`
   - `Undocking`
   - `InFlight`
   - `Docking`
2. Keep a compatibility `IsTraveling` getter if useful, derived from phase.
3. Add a small docking-port/pose seam:
   - logical `CurrentDock` remains the station/facility identity;
   - a dock/port provides physical approach position/orientation.
4. Add one authoritative arrival commit API, e.g. `TryCompleteArrival(owner, dock)`:
   - only the current movement lease owner may commit;
   - commit only after physical movement/docking reports completion.
5. Missing/disabled movement component must block with an inspectable reason.
6. If tests need teleporting, add an explicit instant-movement test implementation; never use absence of movement as success.
7. Add startup diagnostic when initial logical dock and ship transform are materially inconsistent.
8. Correct the current Mining Ship authored start position to match its logical initial dock or the new docking pose.

## Preserve

- `ShipMovementOwner` lease.
- Transport/Extraction/CrewReturn ownership categories.
- Extraction remains outside freight contracts.

## Tests

- two-tick artificial docking: unload/boarding cannot happen before hard dock;
- no mover => no arrival/transfer for transport;
- no mover => no arrival/transfer for extraction;
- only movement owner can commit arrival.

## Acceptance

- [ ] Docking/undocking has real state.
- [ ] Future docking visuals can take time without making dispatch logic lie.
- [ ] Missing movement blocks rather than teleports.
- [ ] Mining ship's initial logical and visible location agree.
