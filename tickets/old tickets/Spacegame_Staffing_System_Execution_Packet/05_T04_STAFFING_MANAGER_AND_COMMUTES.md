# T04 — Staffing Manager, Assignments, Explicit Shifts, and Physical Commutes


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

Make employment and shifts actually drive colonist activity and passenger transport.

Do not implement automatic job allocation. The system executes explicit assignments.

## Create `StaffingManager`

Singleton MonoBehaviour on existing Managers object.

Register with SimulationManager as an `ISimulationTickable`.

Responsibilities:

- validate/apply employment assignments;
- enforce one active job per colonist;
- manage pending reassignment;
- interpret active shifts;
- set worker activity state;
- create/group commute passenger contracts;
- provide assignment queries for StaffingComponent / future UI.

It does **not**:

- choose jobs automatically;
- optimize shift coverage;
- operate facilities;
- move Shuttle directly.

## Assignment API

Provide at minimum:

```text
AssignmentResult Assign(
    ColonistAgent colonist,
    StaffingComponent workplace,
    StaffingRoleDefinition role,
    string shiftId)

AssignmentResult Unassign(ColonistAgent colonist)

IReadOnlyList<ColonistAgent> GetEligibleColonists(
    StaffingComponent workplace,
    StaffingRoleDefinition role)

IReadOnlyList<ColonistAgent> GetAssignedWorkers(
    StaffingComponent workplace,
    StaffingRoleDefinition role,
    string shiftId)
```

Validation:

- role is offered by workplace;
- shift ID exists in workplace pattern;
- colonist has role.requiredClass;
- assigned count for that role+shift is below `maximumAssignedPerShift`;
- colonist exists.

## Reassignment semantics

### Safe at home

If colonist:

- at their home;
- not currently an active passenger;
- not currently working elsewhere;

apply new employment immediately.

### Away / working / in transit

Store requested assignment as `pendingEmployment`.

Continue current employment until worker returns home.

Once at home with no active passenger contract:

```text
currentEmployment = pendingEmployment
pendingEmployment = null
```

Then new shift scheduling takes over.

For Unassign while away:

- create/retain a pending "unassigned" state;
- current assignment remains until return home;
- then clear current employment.

Do not teleport.

## Schedule evaluation

For each currently employed colonist:

Determine if their assigned shift is active at current simulation time.

### Shift active + at workplace

```text
activity = Working
```

### Shift active + at home

```text
activity = WaitingForTransport
request home -> workplace
```

### Shift inactive + at workplace

```text
activity = WaitingForTransport
request workplace -> home
```

### Shift inactive + at home

```text
activity = Resting
```

### Active passenger contract / aboard vehicle

Do not override `Passenger`.

### Unexpected third location

Do not invent complex recovery routing in this ticket.

Set a clear observable staffing/schedule blocker and log once; do not teleport.

## Commute grouping

Do not create one contract per worker unnecessarily.

On a staffing tick:

Group colonists who need the same:

```text
source LocationAnchor
destination LocationAnchor
commute priority
```

and who do not already have an active passenger contract.

Create one passenger contract containing that group, respecting Shuttle passenger capacity through existing logistics behavior.

If existing passenger contract creation cannot split groups above capacity, split deterministically into legal groups.

## Commute priority

The staffing model exposes a player-facing commute priority:

```text
1..10
```

on `StaffingComponent` or its schedule config.

The current repo may still use legacy contract priorities such as 100/90/60.

If global 1..10 logistics has NOT already landed, use one isolated compatibility conversion at the ContractManager boundary:

```text
legacyPriority = staffingPriority * 10
```

Mark it clearly:

```text
// TEMP COMPAT: remove when transport priorities are natively 1..10
```

Do not spread legacy scaling throughout staffing code.

If logistics is already 1..10, pass through directly.

## Late arrival

If a worker reaches the workplace after shift start:

- immediately becomes Working if shift still active;
- works only the remainder of the shift.

If the worker arrives after their shift has already ended:

- do not grant phantom work;
- next staffing tick should request return home.

## Shift changes

At exact shift boundaries, staffing decisions derive from absolute simulation time, not from how long an individual worker happened to work.

No per-worker "8 hours completed" counter.

## Logging

State-transition logging only:

```text
Alice assigned to Farm / Farm Operator / Shift A
Alice waiting for commute to Farm
Alice began Shift A at Farm
Alice shift ended; return requested
Alice returned home
Alice pending assignment changed to Water Processor...
```

Avoid per-tick spam.

## Tests

- wrong-class assignment rejected;
- second current job rejected/replaced only through defined assignment semantics;
- role+shift capacity enforced;
- active shift at home produces commute demand;
- inactive shift at workplace produces return demand;
- at workplace during active shift becomes Working;
- at home off-shift becomes Resting;
- late arrival works remaining shift only;
- reassignment while working becomes pending;
- pending assignment applies at home;
- two workers same route are grouped;
- workers on opposite shifts generate different commute windows.

## Guardrails

- No auto-assignment.
- No "balance shifts" algorithm.
- No overtime/fatigue.
- No ship crew shifts.
- No UI.

## Acceptance

Explicit employment + explicit shift must be sufficient to make qualified humans physically commute and become Working only while on duty at their workplace.
