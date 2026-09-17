# T02 — Employment Capacity, Fixed Crew Base, and Authored Employment Validation

## Goal

Make persistent employment invariants survive disable/re-enable and scene authoring.

## Changes

### A. Disabled worker still consumes assignment capacity

Capacity is based on persistent employment, not whether a colonist GameObject is currently enabled.

- `CountAssigned` / capacity validation must include employed colonists even if temporarily disabled.
- Tick/reconciliation registries may still skip inactive workers for simulation work; do not confuse tick participation with employment ownership.

### B. `crewChangeBase` must be authored/fixed

- Remove behavior where assigning the first pilot silently initializes a null crew-change base from that colonist's home.
- A ship requiring a crew base but missing one should reject the assignment with a clear ship-authoring diagnostic.
- Keep existing authored bases unchanged.

### C. Validate scene-authored employment at startup

- Existing serialized employment should pass through a non-mutating validation pass on startup.
- Invalid authored employment must produce a durable/clear diagnostic naming colonist, workplace, role, shift, and reason.
- Do not silently rewrite valid assignments.

## Tests

- `DisabledWorkerStillCountsTowardCapacity`
- `PilotAssignmentRejectsShipWithoutCrewChangeBase`
- `AuthoredEmploymentValidationReportsInvalidAssignment`

## Acceptance

- [ ] Disable one of N max-cap workers; assigning N+1 is still rejected.
- [ ] Re-enable cannot create over-capacity employment.
- [ ] Null crew base is an authoring failure, not inferred from first pilot.
- [ ] Valid scene-authored employment remains unchanged.
- [ ] Invalid authored employment is visible immediately.
