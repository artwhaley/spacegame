# T08 — Ship crew assignment model

Status: implemented; Unity EditMode validation remains pending because the
project is open in another editor instance. This ticket supersedes the deleted
single-pilot T08 design and the partial implementation made from it.

## Outcome

A shuttle is a staffed workplace with one Pilot role and explicit shift slots. A pilot keeps that employment while off duty, but no sleeping or off-shift pilot owns or reserves the shuttle. The ship separately records the one pilot who is physically responsible for its current operation.

This ticket establishes the data model and assignment rules. T09 implements physical handover and flight behavior. T10 migrates content and proves acceptance.

## Locked terminology and ownership

- **Crew assignment** is persistent employment: `ship crew staffing + role + shift`. It survives shift end, sleep, disembarkation, and ordinary duty cycles.
- **Responsible pilot** is a temporary physical-duty lease held by at most one colonist. It begins on boarding and ends on disembarkation after safe release. It is never inferred from employment alone.
- **Crew-change base** is a fixed `LocationAnchor` on the ship. All colonists assigned to any role on that ship must have that exact anchor as `ColonistAgent.home`.
- **Current dock** is where the ship is physically docked now. It may differ from the crew-change base during an operation.
- `LogisticsManager` dispatches transport work. It does not employ pilots, choose shifts, or own the crew roster.

## Required model

1. Put a normal `StaffingComponent` on each crewed ship and use the existing `EmploymentAssignment(workplace, role, shiftId)` representation. A ship assignment is identified by the assigned staffing component being colocated with a `ShipComponent`/`ShipCrewDutyComponent`. Do not retain a second authoritative ship-employment representation.
2. Remove `EmploymentAssignmentKind.ShipPilot`, `EmploymentAssignment.ship`, the ship-specific constructor, and all branching based on that kind after serialized scene migration is complete. There must be one employment model.
3. Replace these `ShipComponent` authoring fields: `assignedPilot`, `requiredPilotClass`, `pilotRole`, `pilotShiftPattern`, and `pilotShiftId`. The ship instead references its `crewStaffing`, `crewChangeBase`, and the role that controls flight (`operatingRole`). `crewStaffing.shiftPattern` defines the available shifts; `operatingRole.requiredClass` defines qualification.
4. Replace all uses of `assignedPilot` with one of two explicit queries:
   - assigned crew come from `StaffingManager.CollectAssignedWorkers(crewStaffing, role, shiftId, ...)`;
   - the physical operator comes from `ShipCrewDutyComponent.ResponsiblePilot`.
5. The shuttle Pilot role has `maximumAssignedPerShift = 1` and requires the real Pilot class. Therefore an A/B pattern accepts at most two assigned pilots and an A/B/C pattern accepts at most three. Empty shifts are valid and leave the ship uncovered. Do not impose a one-pilot total limit across all shifts.
6. A ship has one active flight-control position in this patch. Multiple simultaneous pilots are rejected even if shift windows overlap. Preserve role-based staffing so later ships can add other crew roles or raise their per-shift capacity without replacing employment again.

## Assignment API and atomic validation

Use the ordinary `StaffingManager.Assign(colonist, crewStaffing, role, shiftId)` and `Unassign(colonist)` entry points. Do not create or keep `AssignPilot` or `EnsurePilotAssignment`.

Before mutating employment, validate all existing facility rules plus the following ship rules:

1. The staffing component belongs to exactly one enabled or disabled `ShipComponent` on the same GameObject.
2. `crewStaffing`, `operatingRole`, its non-null required class, and the requested shift are valid.
3. The colonist has a non-null home.
4. If `crewChangeBase` is null and this is the ship's first crew assignment, stage the colonist's home as the base and commit it only when the assignment succeeds.
5. If `crewChangeBase` is already set, the colonist's home must be the same `LocationAnchor` reference.
6. Per-role/per-shift capacity is available. Assignments on other shifts do not occupy this slot.
7. The colonist has the role's required class.

Return named `AssignmentResult` values for missing home and crew-base mismatch. Log a useful rejection containing colonist, ship, requested role/shift, expected base, and actual home. Any rejection preserves the previous employment, activity, responsible-pilot lease, and configured base.

Assignment or reassignment changes `currentEmployment` immediately. If the colonist is the responsible pilot of an old ship, mark that physical duty for release but keep the old ship's responsible-pilot lease until T09's safe release completes. Employment must never be rolled back to represent that temporary responsibility.

Unassigning the final crew member does not clear `crewChangeBase`. A base is ship configuration after it is established.

Because `ColonistAgent.home` is currently public authoring data, assignment-time validation alone is insufficient. On every crew reconciliation, validate that every assigned crew member still has `home == crewChangeBase`. An invalid member cannot board, cannot become responsible pilot, and forces a stable diagnostic. If that member is already responsible, request safe release to the existing crew-change base. Do not silently rewrite home, base, or employment. Any future home-changing UI/API must call the same validation before committing a home change.

## Duty records

Retain direct duty history. A ship duty record stores the ship, crew staffing component, role, and shift independently from current employment, so reassignment cannot erase the identity of work still being completed. Keep release-request time/reason separate from actual end time/reason.

`OnDutyCrew` remains an activity and never proves employment. Fatigue is charged from an active physical duty record exactly once per simulation tick.

## Lifecycle requirements

- Adding or enabling ship/crew components at runtime discovers existing assignments without reapplying authored startup values.
- Disabling a component pauses its work and preserves employment and the responsible-pilot lease.
- Re-enabling resumes from retained state on the next simulation tick.
- Recreating `StaffingManager` rediscovers assignments and physical leases; it does not board, disembark, reassign, or teleport anyone.
- Do not add assembly definitions or edit generated `.csproj` files.

## Implementation targets

- `Assets/Scripts/ColonyPrototype/People/EmploymentAssignment.cs`
- `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs`
- `Assets/Scripts/ColonyPrototype/People/StaffingComponent.cs`
- `Assets/Scripts/ColonyPrototype/People/ColonistStatusComponent.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/ShipComponent.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/ShipCrewDutyComponent.cs`
- all production callers and focused tests still referring to `assignedPilot` or ship-specific employment

## Essential focused tests

- Assign A, B, and C pilots to one shuttle using a 24-hour three-shift pattern; all three retain employment and each shift reports one assigned pilot.
- A second pilot in the same Pilot/shift slot is rejected atomically, while the same pilot role on another shift succeeds.
- The first successful assignment establishes the base. A different-home assignment is rejected without changing either colonist's employment or the base.
- Changing an assigned pilot's public home later blocks their eligibility and emits the base-mismatch diagnostic.
- Off-shift and sleeping pilots remain assigned but do not become the responsible pilot and do not make the ship operationally crewed.
- Reassignment while physically responsible changes employment immediately and preserves the old physical-duty lease and duty identity for T09 to release.

Run Unity compilation and the focused EditMode tests before proceeding. Compilation alone is not completion.
