# T03 — Staffing Roles, Count Curves, and Facility Performance


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

Build the elegant communication layer between staffing and facility functionality.

This ticket must solve the "slot 1 / slot 2 sliding" problem by making effects depend only on **active qualified worker count**, never worker order.

## Create `FacilityEffectDefinition`

ScriptableObject:

```text
stableId
displayName
description
```

Create assets:

```text
ProductionRate
TreatmentSpeed
TreatmentOutcome
Safety
```

Only `ProductionRate` is used by the current Farm. The others prove the architecture for future facilities without adding gameplay.

All effect values are multiplicative.

Default facility multiplier for an effect with no contribution:

```text
1.0
```

## Create `StaffingRoleDefinition`

ScriptableObject fields:

```text
stableId
displayName
description

WorkerClassDefinition requiredClass

int minimumActiveForOperation
int maximumAssignedPerShift

List<StaffingEffectRule> effects
```

Validation:

```text
minimumActiveForOperation >= 0
maximumAssignedPerShift >= 1
minimumActiveForOperation <= maximumAssignedPerShift
```

## `StaffingEffectRule`

Fields:

```text
FacilityEffectDefinition effect
List<float> multiplierByActiveCount

SkillDefinition bonusSkill          // optional
float maxSkillBonusFraction         // >= 0
```

List index = active worker count.

Example:

```text
index 0 = 0.00
index 1 = 0.65
index 2 = 1.00
index 3 = 1.20
```

If active count exceeds final index, clamp to final value.

Require at least index 0.

Negative multipliers are invalid.

### Skill calculation

Eligible Active workers are those who:

- are assigned to this workplace/role/shift;
- physically present;
- shift active;
- activity `Working`;
- possess requiredClass.

For one effect rule:

```text
base = curve(activeCount)

if bonusSkill != null and maxSkillBonusFraction > 0:
    avg = average skill proficiency across Active workers
    contribution = base * (1 + avg * maxSkillBonusFraction)
else:
    contribution = base
```

Skill never turns an ineligible worker into an Active worker.

## Create `StaffingComponent`

Attach to a workplace/facility.

Fields:

```text
LocationAnchor workplaceLocation
ShiftPatternDefinition shiftPattern
List<StaffingRoleDefinition> offeredRoles
```

Expose queries per role/shift:

```text
AssignedWorkers
PresentWorkers
WorkingWorkers
ActiveQualifiedWorkers
```

Do not store ordered worker slots.

Do not own commuting.

Do not own production.

Use the authoritative employment records from ColonistAgent / StaffingManager.

## Create `IFacilityPerformanceProvider`

Small interface allowing a component to publish:

```text
operational blockers
effect multiplier contributions
```

`StaffingComponent` implements it.

For each offered role:

- if Active < `minimumActiveForOperation`, publish an operational blocker;
- publish each configured effect contribution based on Active count and skill.

A role with minimum 0 never blocks the facility merely because nobody fills it.

## Create `FacilityPerformanceComponent`

Attach to facilities that consume performance.

Responsibilities:

- discover/capture local components implementing `IFacilityPerformanceProvider`;
- evaluate providers on demand;
- expose:

```text
bool IsOperational
IReadOnlyList<string> BlockReasons
float GetMultiplier(FacilityEffectDefinition)
```

Aggregation:

```text
Operational = no provider blockers
Effect multiplier = multiply all contributions for that effect
```

Do **not** hardwire StaffingComponent directly into this class.

Future maintenance/upgrades/buffs must be able to implement the same provider interface.

Avoid tick-order dependence: performance evaluation should be callable synchronously by a consumer immediately before it needs the value.

## Create Farm role asset

```text
FarmOperator
requiredClass = Farm Technician
minimumActiveForOperation = 1
maximumAssignedPerShift = 3

ProductionRate:
    [0] 0.00
    [1] 0.65
    [2] 1.00
    [3] 1.20

bonusSkill = Agriculture
maxSkillBonusFraction = 0.00 for migration parity
```

The skill channel exists but is disabled for current balance.

## Future hospital architecture test

Automated tests should construct definitions equivalent to:

```text
Doctor:
  min active = configurable
  effect = TreatmentOutcome

Nurse:
  min active = 0
  effect = TreatmentSpeed
```

Verify Nurse count changes TreatmentSpeed without modifying TreatmentOutcome and vice versa.

Do not add a hospital GameObject to gameplay.

## Tests

- worker order never affects result;
- 1 Farm Operator -> ProductionRate 0.65;
- 2 -> 1.00;
- 3 -> 1.20;
- 4 active (if test bypasses assignment max) clamps to 1.20;
- zero Farm Operators blocks facility;
- optional Nurse role with 0 active does not block facility;
- Doctor and Nurse effect channels remain independent;
- skill bonus formula works;
- skilled but wrong-class colonist contributes zero;
- two providers contributing same effect multiply.

## Guardrails

- No recipe references.
- No Food/Water references.
- No commute logic.
- No worker slot MonoBehaviour per person.
- No medical gameplay.
- No custom UI.

## Acceptance

This ticket is complete when staffing can answer:

```text
Is the facility operational?
What is its Production Rate multiplier?
What is its Treatment Speed multiplier?
What is its Treatment Outcome multiplier?
```

without knowing anything about recipes or resource types.
