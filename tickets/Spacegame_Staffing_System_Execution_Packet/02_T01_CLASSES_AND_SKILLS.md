# T01 — Worker Classes and Skills


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

Replace the prototype single-role identity model with:

```text
Classes = hard job eligibility, multi-class legal
Skills  = independent 0..1 proficiency bonuses
```

Do not implement facility staffing yet.

## Create ScriptableObjects

### `WorkerClassDefinition`

Fields:

```text
stableId
displayName
description
```

### `SkillDefinition`

Same minimal identity fields.

Stable IDs must be non-empty and unique among committed assets.

## Modify ColonistAgent

Add:

```text
List<WorkerClassDefinition> classes
List<SkillRating> skills
```

`SkillRating`:

```text
SkillDefinition skill
float proficiency
```

Clamp proficiency to:

```text
0..1
```

Helpers:

```text
bool HasClass(WorkerClassDefinition workerClass)
float GetSkill(SkillDefinition skill)
```

Rules:

- duplicate class references are invalid;
- duplicate skill entries are invalid;
- null class/skill entries should be ignored defensively and reported in authoring validation;
- missing skill returns `0`;
- skill does not imply class.

## Create content assets

At minimum:

```text
Assets/GameData/Staffing/Classes/
    Pilot
    BridgeCrew
    Maintenance
    Technician
    FarmTechnician

Assets/GameData/Staffing/Skills/
    Piloting
    Agriculture
    Technical
    WaterProcessing
```

Actual folder naming may follow current repo convention.

## Migrate current eight colonists

Equivalent class migration:

```text
3 bridge crew  -> Bridge Crew
2 pilots       -> Pilot
Maintenance    -> Maintenance + Technician
2 farmers      -> Farm Technician
```

This explicitly proves multiclassing on Maintenance.

Skill values may be zero/default or conservative placeholders. They must not change gameplay yet.

## Legacy ColonistRole

Search all references.

If nothing behavioral still needs `ColonistRole`, delete the enum and field now.

If a later ticket in this packet still temporarily relies on it, mark:

```text
// STAFFING MIGRATION TEMP: delete by T05/T08
```

No new code may depend on `ColonistRole`.

## Tests

- colonist can hold Pilot + Farm Technician simultaneously;
- `HasClass(Pilot)` true only when explicitly classed;
- Agriculture skill 1.0 does not grant Farm Technician class;
- missing skill returns 0;
- proficiency clamps/validates;
- duplicate definitions are rejected/reported.

## Guardrails

- No skill progression.
- No XP.
- No class hierarchy.
- No "skill unlocks class".
- No job assignment yet.
- No UI.

## Acceptance

- current colonists are represented by classes/skills;
- multiclass is proven;
- current game still runs;
- no new gameplay behavior is introduced.
