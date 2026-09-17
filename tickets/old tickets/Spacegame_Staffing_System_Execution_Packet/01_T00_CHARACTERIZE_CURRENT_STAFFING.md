# T00 — Characterize Current Staffing/Shift Behavior


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

Lock down what the current game does before extracting staffing from the Farm.

No intended gameplay change.

## Inspect

At minimum:

```text
ColonistAgent.cs
PopulationManager.cs
FarmController.cs
ContractManager.cs
TransportContract.cs
ShuttleController.cs
SimulationManager.cs
```

## Create

```text
STAFFING_BASELINE.md
```

Document actual current behavior with file references:

1. How the Farm identifies assigned farmers.
2. How the 8h work / 8h rest cycle is represented.
3. Exactly when passenger contracts are created.
4. How a farmer changes among:
   `WaitingForTransport`, `Passenger`, `Working`, `Resting`.
5. Whether all farmers currently move as one group.
6. Current work/rest durations.
7. Current production rate with 0, 1, and 2 physically present farmers.
8. Current Water consumption with 0, 1, and 2 workers.
9. Current passenger contract priority.
10. How Shuttle unload changes passenger activity.
11. Any current code relying on `ColonistRole`.
12. Any current code relying on `assignedWorkplace`.

## Add characterization tests where practical

Do not build a giant scene test harness.

At minimum, add or preserve tests for:

- `PopulationManager` registration.
- passenger contract refuses passenger not at source.
- passenger contract tracks the actual Colonist references.
- a moved colonist's logical `currentLocation` is authoritative.

If existing Unity tests already cover these, reference them instead of duplicating.

## Manual baseline

Run current scene through:

```text
Farmers at Command Post
→ request transport
→ Shuttle carries both
→ both work
→ after work period both request home
→ both rest
→ repeat
```

Record observed timing and current production totals over one 16-hour work/rest cycle.

## Guardrails

- No class/skill code yet.
- No new scheduling.
- No Farm refactor.
- No production changes.
- Fix only defects that make baseline impossible to characterize.

## Acceptance

- project compiles;
- current Phase 2 loop is still functional;
- `STAFFING_BASELINE.md` exists;
- later tickets have a measured parity target.
