# Spacegame — Staffing System Execution Packet


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


## Purpose

Build the production-shaped staffing primitive for the colony simulation.

The system must support:

```text
Colonist
    Classes
    Skills
    One current employment assignment
    One optional pending assignment

Employment
    Workplace
    Staffing Role
    Shift

Workplace
    Staffing Roles
    Shift Pattern
    Assigned / Present / Working workers

Staffing Role
    Required Class
    Minimum Active for operation
    Maximum assigned per shift
    One or more facility-effect curves
    Optional skill bonuses

Facility Performance
    Operational / blocked reasons
    Effect multipliers
```

The core interaction is:

```text
COLONIST
classes + skills + employment
        ↓
STAFFING MANAGER
shift state + physical commute
        ↓
STAFFING COMPONENT
counts active qualified workers per role
        ↓
FACILITY PERFORMANCE
operational gate + effect multipliers
        ↓
CURRENT/FUTURE FUNCTIONAL MODULES
FarmController today
ResourceConverter later
Medical treatment later
Research later
```

## Critical decoupling

A future `ResourceConverterComponent` must only need:

```text
performance.IsOperational
performance.GetMultiplier(ProductionRate)
```

It must never ask:

```text
How many farmers are here?
Who is Bob?
What class is Alice?
What shift is active?
```

Similarly, staffing never knows:

```text
Food
Water
Ice
RecipeDefinition
```

## The shift question is intentionally explicit

For a Farm role with the effect curve:

```text
0 active -> 0%
1 active -> 65%
2 active -> 100%
3 active -> 120%
```

Two workers may be configured:

### Together

```text
Shift A: 2 workers
Shift B: 0 workers
```

Ignoring commute delays:

```text
8h × 100%
8h ×   0%
= 8 equivalent full-output hours per 16h
```

### Opposite shifts

```text
Shift A: 1 worker
Shift B: 1 worker
```

Ignoring commute delays:

```text
16h × 65%
= 10.4 equivalent full-output hours per 16h
```

That is 30% more total throughput than concentrating both workers for half the cycle.

**The staffing system must allow both configurations and must not automatically choose one.**
That decision is gameplay.

## Ticket order

Execute exactly:

1. `01_T00_CHARACTERIZE_CURRENT_STAFFING.md`
2. `02_T01_CLASSES_AND_SKILLS.md`
3. `03_T02_SHIFT_DEFINITIONS_AND_EMPLOYMENT.md`
4. `04_T03_STAFFING_ROLES_AND_FACILITY_EFFECTS.md`
5. `05_T04_STAFFING_MANAGER_AND_COMMUTES.md`
6. `06_T05_FARM_MIGRATION.md`
7. `07_T06_PILOT_CLASS_VALIDATION.md`
8. `08_T07_AUTHORING_AND_OBSERVABILITY.md`
9. `09_T08_ADVERSARIAL_ACCEPTANCE.md`

Read `10_ARCHITECTURE_REFERENCE.md` before implementation.

## Hard scope exclusions

Do not implement:

- staffing UI;
- automatic headcount allocator;
- automatic shift optimizer;
- worker AI choosing jobs;
- two jobs per colonist;
- overtime;
- breaks;
- lunches;
- vacations;
- fatigue;
- sickness;
- skill progression/XP;
- wages;
- morale;
- firing/hiring;
- immigration;
- construction;
- recipe refactor;
- warehouse/buffer storage;
- logistics route optimization;
- ship pilot shift scheduling;
- relief pilot logic;
- general WorkOrder framework.

## Executor discipline

For each ticket:

- inspect actual current files first;
- keep changes ticket-scoped;
- compile before moving on;
- run relevant tests;
- preserve current Food/Water/freight behavior;
- do not solve a later ticket early;
- prefer ordinary serializable data + ScriptableObjects + small MonoBehaviours;
- no custom editor unless absolutely required for correctness.

## Final staffing definition of done

After this packet:

- a colonist may be Pilot + Farm Technician;
- class gates job eligibility;
- skills independently affect configured staffing effects;
- a facility may define multiple distinct roles such as Doctor and Nurse;
- roles are count-based, not ordered slots;
- facilities may require minimum active staff to operate;
- explicit shifts determine who is on duty;
- workers physically commute;
- missing/late workers reduce/block facility performance naturally;
- the Farm gets its output multiplier from generic facility performance;
- putting two farmers on opposite shifts produces the intended continuous lower-rate operation;
- no Recipe data contains staffing requirements;
- no staffing code knows about Food/Water/recipes.
