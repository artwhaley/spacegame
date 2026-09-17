# Staffing Architecture

The staffing system answers one question for a facility without knowing anything
about its recipes or resources: **is it operational, and how well is it
performing right now?**

## The vocabulary

```text
Class      = may do this kind of job         (WorkerClassDefinition)
Skill      = how much bonus they contribute  (SkillDefinition / SkillRating, 0..1)
Role       = what this workplace wants them to do (StaffingRoleDefinition)
Shift      = when they are supposed to do it  (ShiftDefinition inside a ShiftPatternDefinition)
Assignment = workplace + role + shift (EmploymentAssignment); a ship crew
roster is an ordinary workplace
Present    = physically at the workplace
Working    = present + shift active + activity Working
Active     = Working + class-qualified
Effect     = what Active workers do to the facility (FacilityEffectDefinition)
```

`ColonistStatusComponent.CurrentDutyState` is the separate work-obligation
phase used by staffing and future contract policy:

```text
ReleasedResting          no current work obligation
ScheduledShift           assigned shift is active; preparing or commuting
AcceptingNewWork         physically ready for new work
CompletingCommittedWork  shift/release boundary reached; finish accepted work
ReturningHome            no new work; travelling to the home/crew-change base
```

This state intentionally does not replace `ColonistActivity`. For example, a
pilot can remain `OnDutyCrew` while its duty state moves from
`AcceptingNewWork` to `CompletingCommittedWork` and then `ReturningHome`.

A colonist carries classes and skills. A workplace offers roles. An assignment
joins a colonist to one workplace, one role, and one shift.

Ships use the same assignment record as facilities: assign a colonist to the
ship's `StaffingComponent`, Pilot role, and named shift. Employment persists
while the pilot is off duty. `ShipCrewDutyComponent.ResponsiblePilot` is a
separate temporary physical lease for the one pilot currently aboard and in
control; `OnDutyCrew` is only that physical activity and never proves
employment. A sleeping or off-shift pilot therefore does not own or reserve
the ship.

Ship docks are separate from ship anchors. The anchor is the physical aboard
location; `ShipComponent.CurrentDock` is the station where a pilot can board or
disembark directly. A pilot at another station requests a normal passenger
contract. A pilot whose shift ends or whose fatigue reaches the exhaustion
threshold stops accepting new work, finishes the accepted operation, then the
`ShipCrewDutyComponent` flies the ship home and disembarks them.

## Why there are no ordered worker slots

There is never a "slot 1 worker" or "slot 2 worker". A role's contribution
depends only on **how many Active qualified workers there are**. The effect curve
is indexed by that count:

```text
Farm Operator, Production Rate:
  index 0 -> 0.00
  index 1 -> 0.65
  index 2 -> 1.00
  index 3 -> 1.20
```

Consequences:

- Reordering workers cannot change the result.
- Removing one of three workers drops the count from 3 to 2 regardless of which
  person left; nothing slides into a vacated slot.
- A count above the last defined index clamps to the final multiplier.

Skills never create eligibility. A `Farm Technician` gate cannot be satisfied by
`Agriculture` skill; the class is a hard requirement.

## Assigned, Present, Working, Active

Only the last one contributes, and it requires all of:

- assigned to this workplace + role (on any shift);
- physically present (`currentLocation == workplaceLocation`);
- the assigned shift is currently active at the simulation hour;
- `activity == Working`;
- holds the role's required class.

A missing or late worker simply lowers the count, so facility performance drops
or blocks naturally. There is no teleporting and no phantom work.

## Facility performance aggregation

Staffing does not call production code. It implements
`IFacilityPerformanceProvider` and publishes:

```text
- operational blockers                (a role below minimumActiveForOperation)
- effect multiplier contributions     (curve(activeCount) scaled by skill bonus)
```

`FacilityPerformanceComponent` collects every local provider and evaluates them
on demand:

```text
Operational = no provider reports a blocker
Multiplier(effect) = product of all provider contributions for that effect (default 1.0)
```

Any component may implement the interface, so maintenance, upgrades, power
quality, or hazards can later contribute to the same channels without staffing
knowing about them. Effects are evaluated synchronously right before a consumer
needs the value, so there is no tick-order dependence.

The consumer only needs:

```text
performance.IsOperational
performance.GetMultiplier(productionRateEffect)
```

It never asks how many farmers are present, who they are, what class they hold,
or which shift is active.

## Fatigue and personal status

`ColonistStatusComponent` owns personal fatigue and duty history. Fatigue is
normalized `0..1` and changes only while the colonist is actually working or
sleeping:

```text
work:   +0.10 per game hour × StaffingRoleDefinition.exertionMultiplier
sleep:  -0.10 per game hour × home HabitationComponent.restfulnessMultiplier
```

The exhaustion latch sets at `0.90`. A worker stops contributing immediately,
the active duty record ends as `Exhausted`, and staffing requests a trip home.
The latch clears at `0.20`, preventing oscillation near the work threshold.
Waiting, travel, Passenger, and Resting do not change fatigue. A ship pilot's
`OnDutyCrew` lease is physical work and uses the pilot role's exertion
multiplier. A completed duty record stores workplace, role, shift, start/end
time, actual worked hours, fatigue at start/release/end, and an end reason;
transport contracts are never work history.

## Why recipes are deliberately separate

Recipes describe **what** converts into what. Staffing describes **who is
available to run the facility**. Putting a class requirement inside a recipe
would weld a workstation to a worker type and make the same conversion
impossible to automate later.

Because of this split, the same conversion recipe is used by:

- an early **staffed** processor: the facility has a required `StaffingComponent`
  role, so `IsOperational` is false without workers;
- a later **automated** processor: no required role/provider exists, so
  `IsOperational` is true and every effect defaults to 1.0.

That is the whole mechanism - "automated" simply means "no required staffing".

## Explicit shifts

Employment includes a shift. `ShiftPatternDefinition` is a repeating 24-hour
daily schedule with explicit windows and midnight wrap-around support. The
canonical `Daily8HourShifts` pattern has `A` at 0..8, `B` at 8..16, and `C` at
16..24. The system never decides whether workers should overlap or alternate;
the assignment decides. Two workers on `Shift A` concentrate output; workers
split across A/B trade peak rate for 8-hour continuity; adding an explicitly
assigned C worker can cover the full day.

`SimulationManager.CurrentGameHour` remains absolute elapsed time. Staffing uses
the `SimulationTime` hour-of-day view only to evaluate daily windows, so A does
not restart at absolute hour 16. The schedule table and logs show day number and
24-hour time while duty and contract records retain absolute hours.

`StaffingManager` turns the assignment into activity and movement on each tick.
Employment mutation is immediate and atomic:

- a valid assignment replaces `currentEmployment` immediately from any location;
- a valid unassignment clears it immediately;
- an invalid request preserves the old assignment and activity;
- an old workplace loses the worker's contribution synchronously;
- an existing passenger flight is immutable and completes normally.

After a flight unloads, the next staffing tick reconciles from the actual landing
location to the current assignment (or home). There is no automatic vacancy
filling or call-in allocator in the current system.

```text
on duty  + at workplace -> Working
on duty  + at home      -> WaitingForTransport, group a home -> workplace commute
off duty + at workplace -> WaitingForTransport, group a workplace -> home commute
off duty + at home      -> Sleeping until fatigue is zero, then Resting
in transit              -> left alone; late arrivals work only the remaining shift
```

Commutes are grouped by source/destination/priority into the fewest passenger
contracts the existing Shuttle can carry.

### Reassignment without teleporting

A colonist has exactly one current assignment. Reassignment never teleports or
rewrites an in-progress flight: the worker finishes that flight, then requests a
new contract from the landing location. If not in transit, staffing requests the
new workplace immediately when its shift is active, or home otherwise.

## Runtime lifecycle

All simulation tickables register through `SimulationManager.RegisterTickable`
from `OnEnable` and unregister from `OnDisable`. Registration is retained even
when a manager is created later; disabled or destroyed components do no work, and
re-enabling resumes retained state on the next tick. Tick priorities are:

```text
100 staffing/activity
200 facility debug, conversion, and consumption
300 policies and logistics planning
400 physical transport and extraction
1000 unclassified future tickables
```

`StaffingManager` discovers already-enabled colonists once when it activates,
while `ColonistAgent` registers directly when created after the manager. This
keeps the registry creation-order independent without a per-tick scene scan.

`FacilityPerformanceComponent` rebuilds its local provider set on every
evaluation. Runtime-added, enabled providers participate on the next evaluation;
disabled or destroyed providers contribute nothing.

## Ship crew, handover, and the fixed base

Each crewed ship has a normal `StaffingComponent` with a Pilot role and an
explicit shift pattern. `StaffingManager.Assign` and `Unassign` are the only
employment APIs; assignment is immediate, atomic, and remains in force across
shift end and sleep. A ship's `crewChangeBase` is fixed once established, and
every assigned crew member must have that exact anchor as `home`.

`ShipCrewDutyComponent` selects exactly one qualified, on-shift assigned pilot
who is physically at the base. It boards them directly and stores the
temporary `ResponsiblePilot` lease. Shift end, exhaustion, reassignment, or an
invalid crew configuration requests release: accepted transport or extraction
finishes, the ship returns crew-only to the base, and the pilot disembarks.
The next eligible shift can then board without changing either employment
record. Empty shifts intentionally leave the ship parked and unavailable.
