# T12 — Scene/Data Asset Migration and Authoring Hygiene


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

Finish wiring the current playable scene entirely through the new definitions/components and ensure the architecture is practical to author in Unity.

This ticket should contain little algorithmic code.

## Required committed content assets

At minimum:

```text
Assets/GameData/Resources/
    Food.asset
    Ice.asset
    Water.asset

Assets/GameData/Classes/
    Pilot.asset
    BridgeCrew.asset
    Maintenance.asset
    FarmTechnician.asset
    Technician.asset

Assets/GameData/Skills/
    Piloting.asset
    Agriculture.asset
    Technical.asset
    WaterProcessing.asset

Assets/GameData/Recipes/
    HydroponicFood.asset
    IceToWater.asset
```

Names may follow repository convention, but stable IDs must be clear and unique.

## Current scene composition audit

Verify these objects:

### Command Post

```text
LocationAnchor
InventoryComponent
HabitationComponent
PopulationResourceConsumer
ResourceStockPolicyComponent
```

### Farm

```text
LocationAnchor
InventoryComponent
StaffingComponent
WorkScheduleComponent
ResourceConverterComponent
ResourceStockPolicyComponent
```

### Water Processor

```text
LocationAnchor
InventoryComponent
ResourceConverterComponent
ResourceStockPolicyComponent
```

### Shuttle

```text
LocationAnchor
InventoryComponent
ShipComponent
ShipMovementComponent
PassengerCarrierComponent
TransportVehicleComponent
TransportExecutorComponent
```

### Mining Ship

```text
LocationAnchor
InventoryComponent
ShipComponent
ShipMovementComponent
ResourceCollectorComponent
ExtractionMissionController
```

### Ice Asteroid

```text
ResourceDeposit
```

## Priority audit

Every editable current priority is 1..10.

No serialized legacy values such as 50/60/90/100 remain.

Current intended relative behavior remains recognizable.

## Disposition

Set Shuttle default:

```text
Neutral
```

Do not change gameplay by selecting a preference unless specifically needed for a parity test.

## Qualification wiring

Pilot 1 and Pilot 2 have Pilot class.

Farm workers have Farm Technician class.

The same Colonist data structure permits additional classes.

Current skills may be placeholder values; they must not unintentionally alter migrated throughput unless the migrated Recipe explicitly configures a bonus.

## Stock policy wiring

Ensure:

- Farm imports Water and exports Food.
- Command Post imports Food and Water.
- Water Processor exports Water.
- Water Processor Ice target/capacity is visible to ExtractionMission but does not accidentally generate ordinary freight demand unless intentionally configured.
- no facility controller manually registers itself as a supplier.

## Background fill demonstration configuration

Do not materially change the normal game loop.

However, prove the feature works in a test or temporary Inspector exercise:

```text
enable background fill on a spare-capacity inventory
→ when no foreground transport work exists
→ Shuttle may move exportable surplus into that inventory
```

Then restore normal intended scene configuration if this buffer is not part of Phase 2 gameplay.

## Inspector hygiene

Component fields should have useful headers/tooltips where ambiguity exists.

Do not create custom inspectors merely for appearance.

A developer clicking a facility should understand:

- inventory;
- stock policy;
- active recipe;
- staffing;
- blocked reason;
- priorities.

## Acceptance criteria

- Scene contains no missing references.
- Definitions/assets are committed.
- Current gameplay is entirely wired through new components.
- Phase 2 can be run without touching source.
- Authoring a different priority or active recipe is a data/Inspector operation.
