# Target Architecture Reference

This file is descriptive. Tickets are authoritative for implementation order.

## Content definitions (ScriptableObjects)

```text
ResourceDefinition
    stableId
    displayName
    description
    quantityMode: Fractional | Discrete

WorkerClassDefinition
    stableId
    displayName
    description

SkillDefinition
    stableId
    displayName
    description

RecipeDefinition
    stableId
    displayName
    description
    executionMode: Continuous | Batch
    durationHours
    inputs[]
    outputs[]
    staffingRule
```

## Runtime data / pure serializable structures

```text
ResourceAmount
SkillRating
RecipeStaffingRule
ResourceStockPolicyEntry
```

## Runtime MonoBehaviour capabilities

```text
InventoryComponent
LocationAnchor
ResourceDeposit

StaffingComponent
WorkScheduleComponent
HabitationComponent
PopulationResourceConsumer

ResourceConverterComponent
ResourceStockPolicyComponent

ShipComponent
ShipMovementComponent
PassengerCarrierComponent
TransportVehicleComponent
TransportExecutorComponent

ResourceCollectorComponent
ExtractionMissionController
```

## Managers retained / evolved

```text
SimulationManager
SimulationLog
PopulationManager
ContractManager
LogisticsManager
```

## Key dependency direction

```text
RecipeDefinition ──────> ResourceDefinition / Class / Skill definitions

ResourceConverter ────> Inventory + Recipe + optional Staffing

StockPolicy ──────────> Inventory + Location
                           ↓
                     LogisticsManager
                           ↓
                  TransportVehicle/Executor

ExtractionMission ────> ShipMovement + ResourceCollector
                           ↓
                     ResourceDeposit
                           ↓
                        Inventory
```

## Important separation

`ResourceConverterComponent` creates/consumes resources in its own local inventory. It does not know who will transport outputs.

`ResourceStockPolicyComponent` describes how much of each resource this inventory wants to import, retain, export, or accept opportunistically.

`LogisticsManager` turns active stock pressure into transport work.

`ExtractionMissionController` remains a specialized resource-acquisition activity, not ordinary freight.
