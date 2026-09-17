# T13 — Delete Legacy Controllers, Bridges, and Hardcoded Content Logic


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

Remove transitional compatibility code so the refactor finishes with one authoritative architecture rather than old + new systems living together.

## Repo-wide searches

Search for and classify every reference to:

```text
ResourceType
FarmController
WaterProcessorController
CommandPostController
ShuttleController
MiningShipController
assignedShuttle
foodSourceFarm
waterSource
RegisterFreightSupply
ResourceType.Food
ResourceType.Water
ResourceType.Ice
priority = 50
priority = 60
priority = 90
priority = 100
```

Names may differ if earlier tickets already removed them.

## Required removals

Delete named prototype controllers once their replacement components are authoritative.

Delete temporary adapters created during migration.

Remove source-specific resource references from consumers.

Remove hardcoded per-resource Shuttle cargo display/state fields; Inspector should read Inventory generically.

Ensure producers do not manually publish supply from production code.

Ensure no new code depends on old Colonist role enum.

## Allowed remaining literal resource knowledge

Content setup/tests may deliberately refer to specific Food/Ice/Water `ResourceDefinition` assets.

Generic runtime algorithms may not contain branches such as:

```csharp
if (resource == Food) ...
if (resource == Water) ...
```

unless there is an explicitly documented game rule truly unique to that resource (none are expected in this refactor).

## File/folder hygiene

Move new code into sensible capability/content folders if earlier tickets left it alongside prototype files.

Do not perform namespace/package churn solely for aesthetics.

Remove dead code and unused serialized fields.

Fix broken comments that still describe Phase 1 assumptions.

## Compile warnings

Resolve warnings introduced by this refactor.

Do not perform unrelated warning cleanup across packages/vendor code.

## Tests

Run full test suite.

Run manual Phase 2 loop.

## Acceptance criteria

- Legacy named production/ship controllers are deleted.
- `ResourceType` is gone.
- temporary compatibility adapters are gone.
- no duplicated old/new source of truth remains.
- project compiles cleanly.
- full Phase 2 loop still runs.
