# T02 — Shift Patterns and Employment Assignment Model


## Repository and baseline

Repository: `https://github.com/artwhaley/spacegame`  
Target at packet authoring: `main`  
Unity: `6000.5.9f1`

This packet is **staffing only**. It is intentionally independent of the earlier production-refactor packet.

Before editing, inspect the current repository. The packet was authored against a baseline where:

- `ColonistAgent` contains `ColonistRole`, `home`, `currentLocation`, `assignedWorkplace`, and `ColonistActivity`.
- `PopulationManager` is a registry of colonists.
- `FarmController` currently bundles:
  - assigned farmers,
  - an 8-hour work / 8-hour rest state machine,
  - passenger contract generation,
  - Water demand,
  - Water consumption,
  - Food production.
- `ContractManager.CreatePassengerContract(...)` already moves a list of colonists between two logical locations through the existing Shuttle.
- `ShuttleController` currently has one persistent `assignedPilot` and physically carries passenger contracts.
- Production is still Farm-specific on this baseline. This packet **does not require RecipeDefinition or ResourceConverter to exist**.

If the branch has advanced, preserve the architectural requirements in this packet and adapt to the actual code. Do not recreate obsolete controllers merely because a ticket names them.

## Non-negotiable staffing decisions

1. **Recipes do not know about staffing.**
   Staffing is a workplace/facility concern.

2. **No ordered worker slots.**
   There is never a semantic "slot 1 worker", "slot 2 worker", etc. Worker contribution depends on the number of qualified people actively working a role.

3. **Staffing roles are distinct from worker classes.**
   Example:
   - Class: `Farm Technician`
   - Facility role: `Farm Operator`
   A role declares which class is eligible.

4. **Classes are hard eligibility.**
   A colonist may hold multiple classes.
   A colonist without the required class cannot fill that role regardless of skill.

5. **Skills are independent bonuses.**
   Skills do not grant eligibility.
   Skill proficiency is normalized `0..1`.

6. **One colonist has one active employment assignment at a time.**
   Multi-classing means they are eligible for different jobs, not that they work two jobs simultaneously.

7. **Shifts are explicit.**
   Employment is:
   `workplace + staffing role + shift`.
   The system does not secretly decide whether workers should overlap or alternate.

8. **Physical presence matters.**
   Assigned is not Present.
   Present is not Working.
   A worker contributes only when physically at the workplace and currently on-duty for their shift.

9. **Staffing publishes facility effects; functional modules consume effects.**
   Staffing does not directly call production code.

10. **Facility effects are multiplier channels.**
    Examples:
    - Production Rate
    - Treatment Speed
    - Treatment Outcome
    - Safety
    Future maintenance/upgrades/buffs may publish to the same channels.

11. **Operational gates are separate from effect multipliers.**
    A role may require at least N active workers for the facility to be operational.
    Optional roles may instead only modify an effect.

12. **Automated facilities remain possible.**
    A facility without required staffing is operational without workers. This supports an early staffed Ice→Water processor and a later automated processor using the same conversion recipe.

13. **Vehicle crew scheduling is not part of this packet.**
    Existing pilots remain assigned using current vehicle semantics.
    Vehicles may validate that their assigned pilot has the Pilot class, but do not use facility shift scheduling yet.

14. **No staffing UI in this packet.**
    Another workstream may build UI against the APIs introduced here.


## Objective

Define explicit shifts and a colonist employment record without yet moving workers automatically.

The model must make:

```text
Facility + Role + Shift
```

the authoritative employment relationship.

## Create `ShiftPatternDefinition`

ScriptableObject:

```text
stableId
displayName
cycleHours
List<ShiftDefinition> shifts
```

`ShiftDefinition`:

```text
shiftId
displayName
startHour
durationHours
```

Validation:

- `cycleHours > 0`
- unique non-empty shift IDs
- `0 <= startHour < cycleHours`
- `0 < durationHours <= cycleHours`

Support wraparound shifts if duration extends beyond cycle end.

Method:

```text
bool IsShiftActive(string shiftId, float absoluteGameHour)
```

Use positive modulo by cycle length.

Do not assume a 24-hour day.

## Create committed pattern

```text
TwoShift8x8
cycle = 16h
Shift A = start 0, duration 8
Shift B = start 8, duration 8
```

This intentionally reproduces the prototype's 8 work / 8 rest rhythm while allowing opposite coverage.

## Create `EmploymentAssignment`

Serializable runtime record:

```text
StaffingComponent workplace
StaffingRoleDefinition role
string shiftId
```

Because `StaffingRoleDefinition` arrives in T03, create the type reference only after/alongside a minimal forward compile arrangement. If needed, T03 may complete this field in the same commit boundary; do not invent a placeholder string role identity.

## Colonist employment state

`ColonistAgent` gains:

```text
currentEmployment
pendingEmployment
```

Each may be null/unassigned.

Expose read-only convenience properties.

One colonist can have at most one current and one pending assignment.

## Employment does NOT own classes

Do not copy class/skill values into assignments.

Eligibility is always evaluated against the current ColonistAgent.

## No scheduling yet

Do not generate passenger contracts.

Do not change ColonistActivity automatically in this ticket.

Do not remove Farm's current shift loop yet.

## Tests

- Shift A active at hours 0..8 and repeats at 16..24.
- Shift B active at 8..16.
- wraparound shift calculation is correct.
- unknown shift ID is invalid.
- one employment record contains workplace + role + shift only.
- one colonist cannot store multiple current jobs.

## Acceptance

- explicit shift content exists;
- employment structure exists;
- current game behavior remains unchanged.
