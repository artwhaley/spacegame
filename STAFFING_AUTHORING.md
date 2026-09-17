# Staffing Authoring Guide

Everything below is Inspector/data work. No C# is required to add classes,
skills, effects, shifts, roles, or to staff or automate a facility.

## Add a worker class

1. In the Project window choose `Create > Asteroid Colony > Worker Class Definition`.
2. Set a unique `stableId` and a `displayName` (for example `Farm Technician`).
3. Save it under `Assets/GameData/Classes/`.

## Add a skill

1. Choose `Create > Asteroid Colony > Skill Definition`.
2. Set a unique `stableId` and `displayName` (for example `Agriculture`).
3. Save it under `Assets/GameData/Skills/`.

## Make a colonist multi-class

On a `ColonistAgent`, add one or more `WorkerClassDefinition` assets to
`classes`. A colonist may hold `Pilot` and `Farm Technician` at the same time.
Add `SkillRating` entries to `skills`; proficiency is normalized `0..1`.

- Duplicate class or skill entries are invalid and reported in the Console.
- Null entries are ignored defensively and reported.
- A skill never grants a class, and a missing skill reads as `0`.

## Add a facility effect channel

1. Choose `Create > Asteroid Colony > Facility Effect Definition`.
2. Use a stable id such as `production-rate` and a name such as `Production Rate`.
3. Save it under `Assets/GameData/Effects/`.

Existing channels: `Production Rate`, `Treatment Speed`, `Treatment Outcome`,
`Safety`. All multipliers default to `1.0`.

## Create a shift pattern

1. Choose `Create > Asteroid Colony > Shift Pattern Definition`.
2. Add daily shifts. Every pattern repeats on the fixed 24-hour day; there is no
   configurable cycle length. `Daily8HourShifts` is committed at
   `Assets/GameData/Shifts/Daily8HourShifts.asset`:

```text
Shift A: startHour 0, durationHours 8
Shift B: startHour 8, durationHours 8
Shift C: startHour 16, durationHours 8
```

Shifts may wrap past midnight (for example start 20, duration 8). A workplace
with only A assigned is uncovered for 16 hours; A+B is uncovered for 8 hours;
A+B+C can cover the full day. The system never fills an unassigned shift.

## Create a staffing role

1. Choose `Create > Asteroid Colony > Staffing Role Definition`.
2. Set `requiredClass`, `minimumActiveForOperation`, and `maximumAssignedPerShift`.
   Every role must reference a real `WorkerClassDefinition`; null is invalid.
   Set `exertionMultiplier` to tune how tiring the role is (`1.0` is the default).
3. Add one `StaffingEffectRule` per effect channel with a `multiplierByActiveCount`
   curve indexed by active worker count.

`FarmOperator` is committed at `Assets/GameData/Roles/FarmOperator.asset`:

```text
requiredClass            = Farm Technician
minimumActiveForOperation = 1     (a required role: absence blocks the facility)
maximumAssignedPerShift   = 3
Production Rate curve     = [0, 0.65, 1.0, 1.2]
bonusSkill                = (none); maxSkillBonusFraction = 0 (migration parity)
```

## Add staffing to a facility

1. Add `StaffingComponent` to the facility GameObject.
2. Wire `workplaceLocation` (usually the same `LocationAnchor`), `shiftPattern`,
   and one or more `offeredRoles`.
3. Add `FacilityPerformanceComponent` to the same GameObject so consumers can read
   `IsOperational` and effect multipliers.
4. Point the functional module at it, for example `ResourceConverterComponent`:

```text
performance           = the FacilityPerformanceComponent
productionRateEffect  = the Production Rate channel asset
```

`StaffingComponent` is discovered automatically as a provider.

To inspect everyone at runtime, select `StaffingManager` and use the context
menu `Dump Daily Work/Off-Duty Schedule`. The dump includes every active
colonist, planned work/off-duty windows, current activity, duty state, fatigue,
exhaustion, and the latest actual duty record. Planned off-duty time is not
automatically the same thing as Sleeping.

The duty state is an obligation phase, separate from physical activity:
`ReleasedResting`, `ScheduledShift`, `AcceptingNewWork`,
`CompletingCommittedWork`, or `ReturningHome`.

## Assign a colonist

Use `StaffingManager` on the Managers object:

```csharp
StaffingManager.Instance.Assign(colonist, workplace, role, "A");
StaffingManager.Instance.Unassign(colonist);
```

- Wrong class -> `RejectedMissingClass`.
- Role not offered -> `RejectedRoleNotOffered`.
- Unknown shift -> `RejectedShiftUnknown`.
- Role+shift full -> `RejectedAtCapacity`.

Assignments apply immediately and are atomic. A valid request replaces the
current assignment from any location; an invalid request leaves the old job and
activity untouched. A worker already aboard a passenger flight finishes that
flight unchanged, then staffing requests the next trip from the landing location.
There is no pending-employment state and no automatic vacancy filling.

## Assign ship crew

Compose each crewed ship with `ShipComponent`, `ShipCrewDutyComponent`, and a
normal `StaffingComponent`. Wire the roster's `workplaceLocation` to the ship
anchor, an explicit shift pattern, and the Pilot role. On `ShipComponent`, wire
that roster as `crewStaffing`, the same Pilot role as `operatingRole`, and set
`crewChangeBase` and `initialDock` to the shared habitat anchor. Every assigned
crew member must use that anchor as `home`.

Use the ordinary manager API, exactly as for a facility:

```csharp
StaffingManager.Instance.Assign(colonist, ship.crewStaffing, ship.operatingRole, "A");
StaffingManager.Instance.Unassign(colonist);
```

The assignment persists across shift end and sleep, but it does not reserve
the ship. While docked at the crew-change base, `ShipCrewDutyComponent` boards
one qualified assigned pilot whose named shift is active and who is physically
at the base. A pilot away from base receives a normal passenger commute to the
base only while their shift is active. Shift end, `0.90` fatigue, reassignment,
or an invalid crew configuration stops new dispatch, finishes accepted work,
returns crew-only to base, and disembarks the responsible pilot. Another
assigned pilot can then take the next active shift; an empty shift leaves the
ship parked.

## Fatigue and sleep

Every colonist has a `ColonistStatusComponent` (the Person prefab supplies it).
At defaults, one game hour of `Working` adds `0.10` fatigue and one game hour of
`Sleeping` removes `0.10`. Role `exertionMultiplier` and the home
`HabitationComponent.restfulnessMultiplier` modify those rates. Fatigue reaches
`0.90` -> the worker leaves immediately and their duty ends as `Exhausted`.
Recovery clears the exhaustion latch at `0.20`. Waiting, travel, Passenger, and
Resting do not recover fatigue.

## Make an optional role

Set `minimumActiveForOperation = 0`. Absence then lowers an effect instead of
blocking. Example: `Nurse` with a `Treatment Speed` curve only.

## Make a required role

Set `minimumActiveForOperation >= 1`. Example: `Farm Operator` (min 1) or a
`Doctor` with a `Treatment Outcome` curve. While Active is below the minimum the
facility is non-operational and the functional module stops.

## Make an automated facility

Add no required `StaffingComponent` role (or none at all). The functional module
sees `IsOperational == true` and default `1.0` multipliers, so the same recipe
runs with no workers. Many roles/effect channels work side by side; a `Doctor`
and a `Nurse` publish independent channels without reclassifying each other.

## Inspect at runtime

- **ColonistAgent** shows `classes`, `skills`, current employment, location, activity.
- **ColonistStatusComponent** shows fatigue, exhaustion, active duty, and completed duty history.
- **StaffingComponent** shows a per-role/per-shift debug table: assigned, present,
  working, active-qualified, minimum active, maximum assigned.
- **FacilityPerformanceComponent** shows operational state, block reasons, and
  effect multipliers.
- **StaffingManager** shows employed count and the last scheduling diagnostic.
