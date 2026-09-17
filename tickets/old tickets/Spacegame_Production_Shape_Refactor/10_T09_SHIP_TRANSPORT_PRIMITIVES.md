# T09 — Introduce Ship/Transport Composition and Migrate Shuttle Internals


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

Extract the reusable ship and transport capabilities currently bundled inside `ShuttleController`.

This ticket may keep a thin `ShuttleController` compatibility adapter because `TransportContract` and `LogisticsManager` still reference that type. T10 removes that coupling.

## Create runtime capabilities

### `ShipComponent`

Owns only common ship state:

```text
displayName
operationalEnabled
ColonistAgent assignedPilot
WorkerClassDefinition requiredPilotClass
```

Queries:

```text
HasQualifiedPilot
IsOperationallyCrewed
```

Current required class = `Pilot`.

A multi-class colonist qualifies if they have Pilot among their classes.

No shift logic.

### `ShipMovementComponent`

Fields:

```text
float movementSpeed
```

Provides direct simulation-time movement toward a target position/location using current prototype semantics.

No physics, acceleration, pathfinding, or orbit.

### `PassengerCarrierComponent`

Owns:

```text
int passengerCapacity
List<ColonistAgent> currentPassengers
```

Board/unboard operations enforce physical source/destination location and capacity.

Do not move the assigned pilot into this passenger list.

### `TransportVehicleComponent`

Declares this Ship as a logistics transport asset.

Fields:

```text
ShipComponent ship
InventoryComponent cargoInventory
PassengerCarrierComponent passengerCarrier (optional)
TransportDisposition disposition
bool freightEnabled
bool personnelEnabled
```

Disposition enum:

```text
Neutral
FreightOnly
PersonnelOnly
PreferFreight
PreferPersonnel
```

This component will be registered with LogisticsManager in T10.

### `TransportExecutorComponent`

Owns live execution of one `TransportContract`:

```text
currentContract
travel/load/unload/passenger execution state
```

Uses:

```text
ShipMovementComponent
InventoryComponent
PassengerCarrierComponent
LocationAnchor
```

Preserve current physical load/unload semantics.

## Shuttle migration

Convert Shuttle GameObject to composition above.

Move current Shuttle behavior out of the monolith.

If existing LogisticsManager/ContractManager require `ShuttleController`, leave a temporary adapter:

```text
ShuttleController
    delegates IsAvailable
    delegates StartContract
    delegates cargo capacity
    delegates display name
```

It must contain no duplicate execution algorithm.

Mark clearly:

```text
// TEMP COMPATIBILITY: remove in T10/T13
```

## Cargo capacity

Continue using `InventoryComponent` capacity as the source of truth.

Do not keep separate hardcoded Inspector fields for Food/Ice/Water cargo.

`TransportVehicleComponent.GetFreeCargoCapacity(ResourceDefinition)` delegates to Inventory.

Discrete free cargo is planner-normalized to whole units.

## Availability

A transport vehicle is eligible only if:

```text
Ship operational
qualified Pilot physically assigned/aboard according to current simple convention
no current contract
```

Do not add fatigue/scheduling.

## Tests

- Pilot-class requirement works with multi-class colonist.
- non-Pilot cannot make Shuttle available.
- freight load/unload conservation unchanged;
- passenger board/unboard unchanged;
- discrete cargo capacity never yields fractional shipment;
- disposition fields serialize but T10 owns dispatch behavior.

## Guardrails

- Do not create a universal Module system.
- Do not migrate Mining Ship yet.
- Do not refactor Logistics arbitration yet.
- Do not add fuel/damage.

## Acceptance criteria

- Shuttle runtime behavior is implemented by reusable capabilities.
- Any remaining `ShuttleController` is a thin adapter only.
- Phase 2 Shuttle behavior is unchanged.
