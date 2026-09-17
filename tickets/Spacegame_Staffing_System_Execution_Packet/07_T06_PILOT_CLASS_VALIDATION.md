# T06 — Apply Class Eligibility to Existing Pilots Without Adding Crew Scheduling


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

Make the new Class model real outside the Farm without reopening vehicle crew scheduling.

Existing Shuttle and Mining Ship pilot assignment semantics stay otherwise unchanged.

## Shuttle

Inspect current pilot assignment.

Add/reference:

```text
WorkerClassDefinition requiredPilotClass
```

or use a small shared qualification helper if both vehicle controllers can cleanly reuse it.

Availability must require:

```text
assignedPilot != null
assignedPilot.HasClass(requiredPilotClass)
```

If current controller already has a generic ship/crew abstraction because the branch advanced, apply the requirement there instead.

Do not move Shuttle pilot into StaffingManager.

Do not assign the pilot a facility shift.

## Mining Ship

Apply the same Pilot-class eligibility rule to the current Mining Ship operational check.

Do not alter extraction mission behavior.

## Current scene

Pilot 1 and Pilot 2 already receive Pilot class from T01.

Verify both vehicles remain operational.

Temporarily remove Pilot class from one in PlayMode/test and verify that vehicle reports an understandable blocker and does not start new work.

## Multiclass proof

Temporarily/procedurally give a colonist:

```text
Pilot
Farm Technician
```

Verify:

- they are eligible for pilot assignment;
- they are eligible for FarmOperator employment;
- they still cannot have two simultaneous facility employment assignments through StaffingManager;
- vehicle pilot direct assignment remains a separate current prototype mechanism.

Do not try to solve the ambiguity of being actively employed at a Farm while directly assigned as pilot in this packet. Document it as a known boundary to resolve when vehicle crew scheduling is intentionally designed.

The normal scene must not configure such a contradictory assignment.

## Guardrails

- No pilot shifts.
- No relief crew.
- No pilot commute.
- No deadlock prevention.
- No vehicle staffing role.
- No crew fatigue.

## Acceptance

Class eligibility is the authoritative answer to:

```text
"Can this colonist serve as a pilot?"
```

without expanding staffing scope into vehicle scheduling.
