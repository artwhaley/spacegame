# Staffing Architecture Reference

## Content ScriptableObjects

```text
WorkerClassDefinition
SkillDefinition
ShiftPatternDefinition
StaffingRoleDefinition
FacilityEffectDefinition
```

## Runtime serializable data

```text
SkillRating
EmploymentAssignment
ShiftDefinition
StaffingEffectRule
```

## Runtime MonoBehaviours / managers

```text
ColonistAgent
PopulationManager               existing

StaffingManager                 new
StaffingComponent               new
FacilityPerformanceComponent    new

FarmController                  existing, staffing concerns removed
```

## Suggested data shapes

### WorkerClassDefinition

```text
stableId
displayName
description
```

### SkillDefinition

```text
stableId
displayName
description
```

### SkillRating

```text
SkillDefinition skill
float proficiency // 0..1
```

### ShiftPatternDefinition

```text
stableId
displayName
cycleHours
shifts[]
```

Each shift:

```text
shiftId
displayName
startHour
durationHours
```

A colonist is assigned to exactly one shift in their employment.

### EmploymentAssignment

```text
StaffingComponent workplace
StaffingRoleDefinition role
string shiftId
```

A colonist has:

```text
currentEmployment
pendingEmployment
```

at most one of each.

### FacilityEffectDefinition

One multiplier channel:

```text
stableId
displayName
description
```

Examples:

```text
Production Rate
Treatment Speed
Treatment Outcome
Safety
```

All current effects aggregate multiplicatively. Default is `1.0`.

### StaffingRoleDefinition

Example Farm Operator:

```text
displayName: Farm Operator
requiredClass: Farm Technician
minimumActiveForOperation: 1
maximumAssignedPerShift: 3

effects:
  Production Rate:
      multiplierByActiveCount:
          index 0 = 0.00
          index 1 = 0.65
          index 2 = 1.00
          index 3 = 1.20

      bonusSkill: Agriculture
      maxSkillBonusFraction: 0.00 initially
```

A future Nurse role can instead publish Treatment Speed.
A future Doctor role can publish Treatment Outcome.
Either may have `minimumActiveForOperation = 0` if absence should hurt performance rather than block the entire facility.

## Count evaluation

Workers are never assigned ordinal semantic slots.

For one role:

```text
Assigned = employment points here + role + shift
Present  = Assigned and currentLocation == workplace
Working  = Present and current shift is active and activity == Working
Active   = Working and has required Class
```

Effect curve uses `Active`.

If `Active < minimumActiveForOperation`, Staffing publishes an operational blocker.

If Active exceeds the effect list length, clamp to the final defined multiplier.

## Skill bonus

For one effect rule:

```text
base = multiplierByActiveCount[clampedActiveCount]
averageSkill = average GetSkill(bonusSkill) across Active workers
bonus = 1 + averageSkill * maxSkillBonusFraction
contribution = base * bonus
```

If no bonus skill or max bonus = 0:

```text
contribution = base
```

Skill never makes an ineligible worker Active.

## Facility performance aggregation

`FacilityPerformanceComponent` gathers providers implementing a small interface such as:

```text
IFacilityPerformanceProvider
```

Staffing is one provider.

Future providers might be:

```text
Maintenance
Power quality
Nearby command bonus
Upgrade module
Environmental hazard
```

Operational:

```text
facility operational iff no provider reports a blocker
```

Effects:

```text
result multiplier = product of all provider multipliers for the channel
```

Default effect multiplier:

```text
1.0
```

This is how staffing communicates without directly depending on production or medical systems.

## Shift scheduling

`StaffingManager` owns schedule interpretation and commute generation.

For each employed colonist:

### Shift active + at workplace

```text
activity = Working
```

### Shift active + at home

```text
activity = WaitingForTransport
create/group passenger request home -> workplace
```

### Shift inactive + at workplace

```text
activity = WaitingForTransport
create/group passenger request workplace -> home
```

### Shift inactive + at home

```text
activity = Resting
```

### In Shuttle / active passenger contract

Do not override passenger state.

Late arrivals simply work for the remaining active portion of their shift.

If they arrive after the shift ended, they should be routed home again rather than granted phantom work time.

## Reassignment rule

One colonist may only have one current employment.

If reassigned while safely at home and not in transit:
apply immediately.

If reassigned while away/working/in transit:
store as `pendingEmployment`.

Once the colonist returns home and has no active passenger contract:
apply pending assignment atomically.

This avoids teleportation and mid-shift job switching.

## Vehicles

This staffing packet does not schedule ship crew shifts.

Current vehicle pilot fields remain.

Vehicle controllers may validate:

```text
assignedPilot.HasClass(PilotClass)
```

but do not become workplaces managed by `StaffingManager` yet.
