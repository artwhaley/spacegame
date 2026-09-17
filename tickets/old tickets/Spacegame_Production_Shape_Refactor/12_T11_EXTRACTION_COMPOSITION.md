# T11 — First-Class Extraction Composition and Mining Ship Migration


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

Replace `MiningShipController` with reusable ship movement + a generic resource collector + a dedicated extraction mission controller.

Extraction remains a first-order subsystem, intentionally separate from ordinary freight dispatch.

## Target Mining Ship

```text
Mining Ship
├── LocationAnchor
├── InventoryComponent
├── ShipComponent
├── ShipMovementComponent
├── ResourceCollectorComponent
└── ExtractionMissionController
```

Do **not** add `TransportVehicleComponent` unless the Mining Ship genuinely needs to service ordinary freight. Current Phase 2 does not require that.

## `ResourceCollectorComponent`

Fields:

```text
ResourceDefinition collectableResource
float extractionRatePerGameHour
InventoryComponent destinationCargo
```

Operation:

```text
Collect(ResourceDeposit, deltaGameHours)
```

Requirements:

- target deposit resource must match collector resource;
- deposit extraction enabled;
- cargo has capacity;
- conservation: Deposit -X == Cargo +X.

### Fractional resource collection

Collect proportionally each tick.

### Discrete resource collection

Do not create fractional units.

Maintain an internal progress/accumulator for extraction work.

Example rate:

```text
0.2 Wrench-equivalents/hour
```

does **not** place 0.2 Wrench in Inventory.

Accumulate work until at least one complete unit can be extracted, then request whole units from the Discrete deposit.

Never make Deposit or cargo fractional.

This capability is generic even though current game uses Fractional Ice.

## `ExtractionMissionController`

Owns extraction-trip orchestration, not extraction mechanics.

References:

```text
ShipComponent
ShipMovementComponent
ResourceCollectorComponent
ResourceDeposit targetDeposit

LocationAnchor unloadLocation
InventoryComponent unloadInventory

ResourceStockPolicyComponent destinationStockPolicy (preferred where appropriate)
float fallbackTargetStock (only if needed for compatibility)
```

State may remain:

```text
Idle
TravelingToDeposit
Extracting
Returning
Unloading
```

## Destination pressure behavior

Before launching:

```text
needed = desired destination stock - (destination onHand + cargo already aboard)
```

Also respect destination free capacity.

Do not launch when:

- destination does not currently have useful capacity/need;
- deposit depleted;
- ship uncrewed/unqualified;
- ship disabled.

If destination inventory is full because the downstream Processor is blocked, Mining Ship naturally stops launching.

This is a key decentralization/logistics pressure behavior.

## Pilot class

Mining Ship requires a colonist with `Pilot` class through `ShipComponent`.

No Mining class is required yet.

## Unloading

Physical:

```text
Mining Ship Inventory -X
Destination Inventory +X
```

If destination cannot accept all cargo:

- keep remainder aboard;
- remain unloading;
- no deletion.

## Remove `MiningShipController`

Delete once parity passes.

Do not route extraction through:

```text
TransportContract
LogisticsManager ordinary dispatch
generic WorkOrder
```

## Tests

- current Ice trip parity;
- collector rejects wrong resource;
- destination full prevents launch;
- deposit depletion prevents launch;
- partial final deposit load conserved;
- discrete collector never places fractional Discrete units into cargo;
- ship requires Pilot class;
- unload blockage retains cargo safely.

## Guardrails

- No prospecting.
- No multiple-deposit selection.
- No extraction priority scheduler yet.
- No mining equipment modules framework.
- No fuel.
- No generic WorkOrder.

## Acceptance criteria

- Mining Ship is composition-driven.
- Extraction is a reusable first-class capability.
- Current Phase 2 Ice economy still works.
- No `MiningShipController` remains.
