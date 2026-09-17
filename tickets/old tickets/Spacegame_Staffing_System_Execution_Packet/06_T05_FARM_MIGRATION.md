# T05 — Migrate Farm from Monolithic Shift Logic to Generic Staffing


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

Remove staffing/scheduling responsibilities from `FarmController`.

The Farm keeps its current Water demand and Food production logic for now, but consumes generic Facility Performance instead of directly counting farmers.

This is deliberately **not** a recipe refactor.

## Before editing

Compare against `STAFFING_BASELINE.md`.

The current baseline Farm typically owns:

```text
assignedFarmers
workDurationHours
restDurationHours
FarmShiftState
passenger request methods
farmer activity transitions
CountWorkingFarmersAtFarm
Water demand
Water consumption
Food production
```

After this ticket, the first six categories belong to the staffing system.

## Target Farm components

Add:

```text
StaffingComponent
FacilityPerformanceComponent
```

Keep existing:

```text
LocationAnchor
InventoryComponent
FarmController
```

`FarmController` remains only because production/demand have not yet been generalized.

## Configure StaffingComponent

```text
workplaceLocation = Farm
shiftPattern = TwoShift8x8
offeredRoles = FarmOperator
commutePriority = current high passenger priority represented on 1..10 scale
```

FarmOperator is the T03 asset:

```text
minimum active = 1
max assigned per shift = 3
ProductionRate:
0 -> 0
1 -> .65
2 -> 1.0
3 -> 1.2
```

## Initial employment for parity

Assign both existing farmers:

```text
workplace = Farm
role = FarmOperator
shift = Shift A
```

This preserves the old synchronized:

```text
8h work
8h rest
```

shape for the first parity run.

They must have `Farm Technician` class.

## Remove from FarmController

Remove all ownership of:

- assigned farmer list;
- FarmShiftState;
- work/rest accumulators;
- work/rest duration;
- passenger-contract creation;
- activity-state changes;
- "all farmers at home/farm" helpers;
- current farmer shift timer.

`StaffingManager` owns these now.

## Production integration

FarmController references:

```text
FacilityPerformanceComponent performance
FacilityEffectDefinition productionRateEffect
```

At each production tick:

1. evaluate performance synchronously;
2. if `!performance.IsOperational`:
   - production = 0;
   - expose/log staffing blocker;
3. otherwise:
   ```text
   multiplier = performance.GetMultiplier(ProductionRate)
   ```

### Normalize base rates

Current baseline with 2 working farmers must remain the 100% reference.

If baseline is:

```text
1 Food / farmer / hour
0.5 Water / farmer / hour
```

then convert FarmController configuration to facility-level base rates:

```text
base Food at 100% = 2 Food/hour
base Water consumption at 100% = 1 Water/hour
```

Then:

```text
1 active worker -> 65%
  Food = 1.3/hour
  Water = 0.65/hour

2 active workers -> 100%
  Food = 2/hour
  Water = 1/hour

3 active workers -> 120%
  Food = 2.4/hour
  Water = 1.2/hour
```

Scale both input consumption and output throughput by ProductionRate.

Water starvation still proportionally constrains Food production exactly as current Farm does.

Do not change Water demand/reorder logic in this packet.

## Blocking reason precedence

Expose understandable state:

1. If Facility Performance not operational:
   `"Understaffed: Farm Operator X/Y active"` or equivalent.
2. Else if Water insufficient:
   `"No Water"`.
3. Else:
   operational.

Do not hide staffing blockage behind Water blockage.

## Remove legacy ColonistRole farmer behavior

No Farm code may check:

```text
ColonistRole.Farmer
```

The relevant eligibility is:

```text
Farm Technician class
```

## Parity test: both on Shift A

Ignoring small commute delays, over the same 16-hour schedule:

Old expected:

```text
8h × full two-worker output
8h × zero
```

New must match within epsilon.

## Gameplay test: split shifts

Reassign:

```text
Farmer 1 -> Shift A
Farmer 2 -> Shift B
```

Ignoring commute delays, expected facility effect:

```text
~65% continuously
```

Integrated ideal throughput:

```text
16 × .65 = 10.4 full-output-equivalent hours
```

versus concentrated:

```text
8 × 1.0 = 8
```

The system must therefore expose the expected ~30% ideal throughput advantage before commute delays/resource shortages.

The staffing system must NOT choose this arrangement automatically.

## Skill test

Temporarily configure FarmOperator Agriculture skill bonus for a controlled test.

Verify:

- same class + higher Agriculture raises ProductionRate as defined;
- Agriculture skill on a colonist without Farm Technician class does not allow assignment/contribution.

Return gameplay asset bonus to intended current value (0 if preserving balance).

## Guardrails

- Do not create RecipeDefinition.
- Do not create ResourceConverter.
- Do not touch Water freight architecture.
- Do not add staffing UI.
- Do not add auto-balance.

## Acceptance

- Farm staffing/commuting is completely generic.
- FarmController no longer schedules people.
- Production consumes `FacilityPerformance`, not headcount directly.
- same-shift parity works.
- opposite-shift strategy works.
