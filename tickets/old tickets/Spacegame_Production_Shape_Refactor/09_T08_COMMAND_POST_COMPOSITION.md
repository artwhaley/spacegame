# T08 — Decompose Command Post Consumption and Resupply


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

Replace `CommandPostController` with generic habitation, population consumption, and stock-policy capabilities.

The Command Post remains a special facility conceptually, but Food/Water consumption and resupply must no longer require a special controller.

## Target composition

```text
Command Post
├── LocationAnchor
├── InventoryComponent
├── HabitationComponent
├── PopulationResourceConsumer
└── ResourceStockPolicyComponent
```

Future bridge functionality is out of scope and may later add a separate Command/Bridge capability.

## `HabitationComponent`

Fields:

```text
LocationAnchor location
int capacity
```

For current scene:

```text
capacity = 8
```

Expose current resident count by querying `PopulationManager` for colonists whose `home` is this LocationAnchor.

Do not implement bed assignment or occupancy movement.

## `PopulationResourceConsumer`

Configurable entries:

```text
ResourceDefinition resource
float amountPerResidentPerGameHour
```

References:

```text
HabitationComponent habitation
InventoryComponent inventory
```

Each simulation tick:

```text
required = residentCount * rate * deltaGameHours
```

Consume from local Inventory.

Expose per-resource shortage state.

For Discrete resources, reject fractional consumption configuration unless consumption is explicitly defined in whole batch semantics later; do not invent batch personal consumption now. Current Food/Water are Fractional.

Configure current rates to match pre-refactor Command Post behavior.

## Stock policy

Create entries for Food and Water that preserve current:

```text
reorder thresholds
target stocks
min/max shipment
priority ordering
```

Normalize priorities to 1..10 while preserving relative importance.

Remove source-specific fields such as:

```text
foodSourceFarm
waterSourceProcessor
```

The logistics layer chooses supply.

Do not make Command Post export Food/Water.

Optional background fill can be disabled for parity; architecture must support enabling it later.

## Shortage logging

Preserve existing useful state-transition logs for Food/Water shortage and recovery.

Do not log every tick.

## Remove `CommandPostController`

After parity:

- delete it; or
- retain only a no-logic compatibility adapter until T13 if scene serialization requires an intermediate step.

## Tests / manual parity

- eight residents consume at current Food/Water rates;
- shortage states occur at zero;
- replenishment clears shortage;
- Food/Water demand comes from StockPolicy;
- supply is chosen by logistics, not a direct Farm/Processor reference;
- Farm/Processor resources still physically move by Shuttle before Command Post OnHand increases.

## Guardrails

- No individual hunger/thirst.
- No personal inventories.
- No bridge buffs.
- No health/death.
- No oxygen.

## Acceptance criteria

- Command Post Food/Water behavior is generic composition.
- No source-specific resource controller remains.
- Phase 2 loop still works.
