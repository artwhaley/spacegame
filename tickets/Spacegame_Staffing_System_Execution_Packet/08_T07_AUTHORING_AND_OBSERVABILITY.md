# T07 — Staffing Authoring Guide and Inspector Observability


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

Make the staffing system understandable to the project owner and future content authors without building gameplay UI.

## Create

```text
STAFFING_ARCHITECTURE.md
STAFFING_AUTHORING.md
```

## `STAFFING_ARCHITECTURE.md`

Explain in plain language:

```text
Class      = may do this kind of job
Skill      = how much bonus they contribute
Role       = what this workplace wants them to do
Shift      = when they are supposed to do it
Assignment = workplace + role + shift
Present    = physically at workplace
Working    = present + shift active + activity Working
Active     = Working + class-qualified
Effect     = what Active workers do to the facility
```

Explain why there are no ordered slots.

Explain facility performance aggregation.

Explain why recipes are deliberately separate.

Explain why automated facilities work simply by having no required staffing provider/role.

Explain current vehicle pilot boundary.

## `STAFFING_AUTHORING.md`

Exact steps:

### Add a worker class

Create WorkerClassDefinition asset.

### Add a skill

Create SkillDefinition.

### Make a colonist multi-class

Add multiple class assets to ColonistAgent.

### Create a shift pattern

Example TwoShift8x8.

### Create a staffing role

Example FarmOperator:

```text
class Farm Technician
min active 1
max per shift 3
ProductionRate [0,.65,1,1.2]
```

### Add staffing to a facility

Add:

```text
StaffingComponent
FacilityPerformanceComponent
```

Wire:

```text
LocationAnchor
ShiftPattern
Offered roles
```

### Assign a colonist

Use the StaffingManager API / temporary Inspector data path available in the project.

Describe pending reassignment.

### Make an optional role

Example Nurse:

```text
min active 0
TreatmentSpeed curve
```

### Make a required role

Example Farm Operator:

```text
min active 1
```

### Make an automated facility

Do not add a required StaffingComponent/role, or configure no required roles. Functional module sees FacilityPerformance operational with default effect multipliers.

## Inspector observability

### Colonist

Must make visible:

```text
Classes
Skills
Current employment:
  Workplace
  Role
  Shift

Pending employment
Current location
Activity
```

### StaffingComponent

Must make visible enough runtime debug state to answer:

```text
Role
Shift
Assigned
Present
Working
Active qualified
Minimum active
Maximum assigned
```

This may use serializable debug snapshots refreshed during simulation.

Do not create custom inspectors solely for aesthetics.

### FacilityPerformance

Expose:

```text
Operational
Block reasons

ProductionRate
TreatmentSpeed
TreatmentOutcome
Safety
```

It is acceptable to expose only configured/queried effect snapshots generically rather than hardcode those four fields.

### StaffingManager

Expose:

```text
registered/current employed count
pending reassignment count
last scheduling/assignment diagnostics
```

No per-tick log spam.

## Guardrails

- No Canvas/UI Toolkit game UI.
- No auto-allocator.
- No content unrelated to staffing.

## Acceptance

The project owner can click Farm, a farmer, and Managers in Play Mode and understand exactly:

```text
who is assigned
who is physically present
who is working
why Farm is blocked
what performance multiplier staffing is producing
```
