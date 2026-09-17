# T03 — Classes, Skills, and Staffing Primitives


## Repository / Baseline Context

Repository: `https://github.com/artwhaley/spacegame`  
Target branch at packet authoring: `main`  
Unity: `6000.5.9f1`  
Namespace: preserve the existing `AsteroidColony` namespace unless the repository has deliberately changed it.

This is a **behavior-preserving production-shape refactor** of the working Phase 1/2 prototype. Do not add new gameplay merely because the new architecture makes it possible.

Current architectural facts that matter:

- `SimulationManager` owns the simulation clock and ticks registered `ISimulationTickable` objects.
- `InventoryComponent` is already generic in behavior, but is keyed by the `ResourceType` enum and stores `float` quantities.
- `ResourceDeposit` is already a generic finite deposit, but is keyed by `ResourceType`.
- `FreightDemand` / `FreightSupply` and `LogisticsManager` already provide a useful generic freight-planning spine.
- `TransportContract` currently represents both Freight and Passenger work and stores a `ShuttleController` as the assigned vehicle.
- Concrete controllers (`FarmController`, `WaterProcessorController`, `CommandPostController`, `ShuttleController`, `MiningShipController`) currently combine multiple responsibilities that must be separated into reusable capabilities.
- Phase 2 gameplay currently contains Food, Ice, Water; Command Post, Farm, Water Processor, Ice Deposit; Shuttle and Mining Ship; eight original colonists.

### Global invariants

1. People and resources do not teleport.
2. Inventory is authoritative for local resource ownership.
3. Reservations prevent double-spending.
4. Resource transfers conserve quantity except explicit production/consumption.
5. Finite deposits remain finite.
6. Phase 1/2 behavior must remain functionally equivalent after the refactor.
7. Do not introduce a universal entity/action/work-order framework.
8. Do not add construction, power, oxygen, markets, fuel, repairs, orbital mechanics, exploration, procedural generation, or new population gameplay in this refactor.
9. Keep extraction a first-class subsystem, separate from ordinary freight dispatch.
10. Prefer composition of small MonoBehaviours plus ScriptableObject content definitions over named building/ship controllers.


## Objective

Replace the prototype assumption that one enum role defines a person with two independent systems:

1. **Classes** — hard job eligibility; multi-classing is legal.
2. **Skills** — numeric proficiency used for bonuses.

Introduce a reusable `StaffingComponent` without changing current Phase 2 gameplay.

## Create definitions

Suggested:

```text
WorkerClassDefinition : ScriptableObject
SkillDefinition : ScriptableObject
```

Fields for both:

```text
stableId
displayName
description
```

No skill trees or progression.

## Colonist data

Modify `ColonistAgent` to hold:

```text
List<WorkerClassDefinition> classes
List<SkillRating> skills
```

`SkillRating`:

```text
SkillDefinition skill
float proficiency   // clamp 0..1
```

Required helpers:

```text
HasClass(WorkerClassDefinition)
GetSkill(SkillDefinition) -> 0..1
```

A colonist may have any number of classes.

A Skill **never grants class eligibility**.

## Current class assets

Create at least:

```text
Pilot
Bridge Crew
Maintenance
Farm Technician
Technician
```

Current people migrate approximately:

```text
Pilot 1/2       -> Pilot
Bridge crew     -> Bridge Crew
Maintenance     -> Maintenance + Technician (legal multi-class)
Farmers         -> Farm Technician
```

Do not change their current jobs/behavior.

Create skills sufficient to prove parallel support, e.g.:

```text
Piloting
Agriculture
Technical
Water Processing
```

Assign conservative placeholder proficiencies in scene only if useful for testing. Skill values must not change current production yet.

## `StaffingComponent`

Create a reusable runtime component with:

```text
LocationAnchor workplace
List<ColonistAgent> assignedWorkers
```

Queries at minimum:

```text
AssignedCount
PresentCount
WorkingCount
GetWorkingWorkers()
CountWorkingWithClass(class)
```

"Present" means physically at workplace according to current logical location.

"Working" additionally means the colonist is in the existing working activity/state.

Do not make Staffing decide commuting, sleep, or jobs.

## Recipe staffing data primitive

Create a serializable `RecipeStaffingRule` (even though recipes arrive T04):

```text
minimumWorkers
maximumEffectiveWorkers
requiredClass              // optional when minimumWorkers == 0
preferredSkill             // optional
additionalWorkerBonus      // throughput multiplier added per extra effective worker
maxPreferredSkillBonus     // maximum bonus at proficiency 1.0
```

Rules:

- If `minimumWorkers == 0`, no class is required and production may operate automatically.
- Workers only count toward required staffing if they possess `requiredClass`.
- Extra workers beyond minimum may add the configured bonus up to maximumEffectiveWorkers.
- Skill is bonus-only.
- For preferred-skill bonus, use the average preferred-skill proficiency of effective eligible workers.
- Throughput multiplier starts at 1.0 once minimum staffing is met.

Do not tune gameplay with these yet.

## Legacy role removal

If `ColonistRole` is used only for Inspector labeling after migration, remove it now.

If a current controller still requires it temporarily, retain it as a clearly marked compatibility field only until T06/T13; do not let new systems depend on it.

## Tests

- one colonist can hold Pilot + Farm Technician simultaneously;
- HasClass is exact and independent of skill;
- skill 1.0 without required class does not satisfy class eligibility;
- Staffing counts assigned/present/working correctly;
- staffing rule with minimum 1 rejects zero eligible workers;
- extra-worker and preferred-skill multipliers calculate deterministically.

## Guardrails

- No job allocator.
- No skill progression.
- No XP.
- No class exclusivity.
- No player UI.
- No automatic reassignment.
- Do not change current shift behavior.

## Acceptance criteria

- Multi-class colonists work.
- Class and Skill systems coexist independently.
- Staffing is a reusable capability.
- Current eight-person Phase 2 scene behavior is unchanged.
- Tests pass.
