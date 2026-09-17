# T08 — Adversarial Staffing Acceptance Suite


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

Prove the staffing architecture is correct, decoupled, and usable before adding gameplay UI or warehouse work.

Do not add features in this ticket. Fix defects only.

# A. Class / skill tests

1. Colonist with Farm Technician can fill FarmOperator.
2. Colonist without Farm Technician cannot.
3. Agriculture 1.0 without Farm Technician still cannot.
4. One colonist can be Pilot + Farm Technician.
5. One colonist cannot have two current facility employment assignments.

# B. Role count tests

Using FarmOperator:

```text
0 active -> facility blocked
1 active -> ProductionRate .65
2 active -> ProductionRate 1.0
3 active -> ProductionRate 1.2
```

No result may depend on which named colonist is first/second/third.

Remove "worker 1" while two others remain:
result must stay based on Active count, with no slot sliding operation.

# C. Multiple-role independence test

Create test-only staffing roles:

```text
Doctor -> TreatmentOutcome
Nurse  -> TreatmentSpeed
```

Cases:

```text
Doctor 1, Nurse 0
Doctor 1, Nurse 1
Doctor 1, Nurse 2
```

Verify Nurse count changes only TreatmentSpeed.

Then remove Doctor and verify behavior follows Doctor role's configured minimum/effect, without reclassifying a Nurse as Doctor.

# D. Facility performance provider test

Add a test provider publishing:

```text
ProductionRate × 0.8
```

With Farm staffing at:

```text
ProductionRate × 1.2
```

Facility Performance must return:

```text
0.96
```

This proves future maintenance/upgrades can interact without staffing knowing about them.

# E. Explicit shift test

Two farmers, TwoShift8x8.

## Concentrated

```text
Farmer 1 = Shift A
Farmer 2 = Shift A
```

Expected ideal performance schedule:

```text
A active: 1.0
B active: blocked/0
```

## Staggered

```text
Farmer 1 = Shift A
Farmer 2 = Shift B
```

Expected ideal schedule:

```text
A active: .65
B active: .65
```

The system must not automatically rewrite either assignment.

# F. Physical commute test

At active shift start:

```text
assigned but at home
→ WaitingForTransport
→ passenger contract
→ Shuttle physically moves worker
→ worker unloads at workplace
→ next staffing tick => Working
```

No facility contribution before physical arrival.

At shift end:

```text
Working
→ WaitingForTransport
→ passenger contract home
→ physical return
→ Resting
```

# G. Late transport test

Delay the Shuttle until halfway through the shift.

Worker must:

- remain Assigned;
- remain not Active while absent;
- begin Working upon late arrival if shift still active;
- stop at normal shift end;
- receive no compensation/phantom extra hours.

# H. Missed shift test

Delay worker until shift has ended before arrival.

They must not become Working for the expired shift.

System should route them home according to current schedule.

# I. Pending reassignment test

While Farmer 1 is working:

```text
Assign to another valid workplace/role
```

Expected:

```text
current employment unchanged
pending employment populated
```

After physical return home:

```text
pending becomes current
```

No teleportation.

# J. Capacity test

FarmOperator max per shift = 3.

Attempt fourth assignment to same role+shift:

```text
rejected with explicit reason
```

The same worker may be assignable to the same role on another shift only after normal single-employment rules are respected; do not create simultaneous jobs.

# K. Skill bonus test

Set a temporary FarmOperator skill bonus:

```text
Agriculture max +20%
```

At one Active worker with skill .5:

```text
base .65
bonus factor 1.10
expected .715
```

within float epsilon.

Wrong-class skillful colonist contributes nothing.

# L. Farm parity

Restore migration configuration:

```text
both farmers Shift A
skill bonus 0
```

Over an ideal 16h cycle excluding measured commute delay:

new full-staff work-period rate must match the T00 baseline.

Water/Food logistics must remain functional.

# M. Staggered gameplay result

Set opposite shifts.

Verify integrated ideal production is consistent with:

```text
16h × .65 = 10.4 equivalent full-output hours
```

rather than:

```text
8h × 1.0 = 8
```

Actual observed delivered Food may differ because travel and Water logistics are real. Document those differences instead of "correcting" them away.

# N. Automated-facility architecture proof

Create test-only facility GameObject:

```text
FacilityPerformanceComponent
```

with no Staffing provider.

Expected:

```text
Operational = true
ProductionRate default = 1.0
```

This proves a future automated processor can use the same functional module/recipe without staff.

# O. Recipe independence assertion

Repo search:

Staffing code must have zero compile-time references to:

```text
Food
Water
Ice
ResourceType (except unrelated legacy elsewhere)
RecipeDefinition
ResourceConverter
FarmController production internals
```

FarmController may query FacilityPerformance; Staffing must not call FarmController.

# P. Current vehicle sanity

Current Shuttle and Mining Ship:

- operate with Pilot-class assigned pilots;
- refuse new operation if assigned pilot lacks Pilot class;
- otherwise preserve existing behavior.

No crew shift scheduling is expected.

# Final pass conditions

- automated tests pass;
- no recurring Console errors;
- Farm loop still operates;
- staffing is count-based, never ordered slots;
- shifts are explicit;
- class/skill separation works;
- multiple facility roles/effect channels work;
- facility effects are decoupled from functional modules;
- docs are complete.

# Stop

After this passes, STOP.

Do not implement staffing UI or warehouse storage automatically.
