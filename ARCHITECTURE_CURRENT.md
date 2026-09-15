# Current Architecture (Phase 2 Baseline)

This is the T00 characterization of the working scene before the production-shape refactor. The authoritative runtime code is under `Assets/Scripts/ColonyPrototype` and the current scene is `Assets/SpaceSim.unity`.

## Simulation and registration

`SimulationManager` owns `currentGameHour`, `currentTick`, an accumulator, and the real-time loop. Every `tickIntervalSeconds` (0.1 seconds in the scene), it advances game time by `gameHoursPerRealSecond * speedMultiplier * tickIntervalSeconds`, increments the tick, and calls `SimulationTick(deltaGameHours)` on registered `ISimulationTickable` instances in registration order. `SimulationManager.Register` and `Unregister` maintain a de-duplicated in-memory list; there is no scene lookup per tick.

The manager objects register from `Start`: `LogisticsManager`, `CommandPostController`, `FarmController`, `WaterProcessorController`, `ShuttleController`, and `MiningShipController`. Population registers `ColonistAgent` instances with `PopulationManager` from `Start` and unregisters them on destroy. Controllers unregister from `OnDisable`.

## Inventory and conservation

`InventoryComponent` stores a serialized list of `InventoryEntry` records keyed by `ResourceType`. Each entry has `onHand`, `reserved`, `capacity`, and derived `available = onHand - reserved`. `Refresh` clamps stock to non-negative values, keeps reservations between zero and on-hand, caps on-hand at capacity, and recalculates available stock.

- `Add` accepts only up to free capacity and returns the actual amount added.
- `Remove` consumes on-hand stock and returns the actual amount removed. If consumption reaches reserved stock, the reservation is reduced to remain valid.
- `Reserve` consumes Available without changing OnHand.
- `ReleaseReservation` restores Available without changing OnHand.
- `WithdrawReserved` decreases both Reserved and OnHand by the physically withdrawn amount.

Inventory is the local authority for ownership. The current implementation uses `float` quantities and has no discrete-resource validation yet.

## Freight and passenger contracts

`ContractManager` owns the serialized `TransportContract` history and generates IDs. Freight creation caps the requested quantity by source Available and destination free capacity, then reserves the source before creating an Open contract. A freight shuttle withdraws the reservation at pickup, adds the actual load to its cargo inventory, and returns any cargo overflow to the source. At the destination it removes cargo and adds it to the destination inventory; if destination capacity is blocked, the cargo remains aboard and unloading retries.

Active inbound accounting uses `TransportContract.RemainingQuantity` (quantity minus delivered quantity). Inbound is not added to destination OnHand before physical unloading.

Passenger contract creation requires every passenger to be physically at the source location. The shuttle moves each passenger to its shuttle anchor while loading and to the destination anchor while unloading. Passenger activity changes to `Passenger` while aboard and `Idle` after arrival.

`FindBestOpenContract` orders Open contracts by numeric priority, then contract ID. At this baseline the two work types are not arbitrated in one vehicle-aware decision: demand reconciliation can create and immediately assign a freight contract, while open passenger contracts are handled by a separate legacy assignment pass.

## Logistics demand/supply

`LogisticsManager` maintains registered `FreightDemand` snapshots and `FreightSupply` sources. A demand publishes desired quantity, priority, minimum/maximum shipment, destination, and resource. On a logistics tick, the manager subtracts active inbound quantity, destination capacity, source Available stock, and an available shuttle's cargo capacity before creating and assigning at most one freight contract per demand. Registered supplies expose their live inventory; reservations therefore reduce what can be exported.

The current direct Food path in `CommandPostController` still creates a freight contract against the configured Farm source. Water demands use the newer demand/supply path. `TryAssignNext` separately assigns the best Open contract to an idle `ShuttleController`, so passenger and freight work do not yet share a single arbitration path.

## Current facilities and people

### Farm

`FarmController` owns shift state, commute passenger contracts, Water demand, Water consumption, and Water-to-Food production. Assigned farmers begin at home, ride to the Farm, and only enter `Working` after all have arrived. During work, each present working farmer consumes `0.5 Water/game hour` and produces `1 Food/game hour` when Water is available. The work shift lasts 8 hours; the rest shift lasts 8 hours and begins only after the return passenger contract completes.

Scene tuning: Farm inventory Food capacity 40 and Water capacity 16; Water reorder threshold 3, target 8, minimum shipment 4, maximum shipment 8, priority 60.

### Water Processor

`WaterProcessorController` consumes Ice and produces Water in its local inventory at `2 Ice/game hour -> 2 Water/game hour`. Ice input target is 24, Ice capacity 48, and Water capacity is 40. It reports input starvation and output blockage and stops processing if there is no Ice or no Water capacity. Water is registered as a freight supply by the controller.

### Command Post

`CommandPostController` counts all registered population as residents under the current Phase 1 simplification, regardless of where they are physically located. It consumes `0.1 Food/game hour` and `0.1 Water/game hour` per resident from the Command Post inventory and logs shortage state transitions. Food reorder threshold is 8, target 20, maximum order 12. Water reorder threshold is 8, target 16, minimum shipment 4, maximum shipment 12, priority 90. Food still has a direct Farm source reference; Water uses the generic demand registry.

Scene tuning: Command Post Food and Water capacities are both 40, with starting stock 12 Food and 16 Water.

### Ships

`ShuttleController` owns movement, availability, passenger boarding, cargo loading/unloading, contract execution, and Inspector observability. It moves directly toward anchors at `12` world units/game hour, has passenger capacity 4, and cargo capacity 12 per configured inventory resource. A shuttle is available only when operational, piloted, and idle.

`MiningShipController` is a specialized finite-Ice extraction loop. It requires an assigned pilot, travels at `12`, mines at `6 Ice/game hour`, carries up to 24 Ice, and targets 24 Ice at the Processor. It checks destination capacity before launching, extracts only from `ResourceDeposit`, returns cargo physically to the Processor, and retains cargo if unloading is blocked. The scene deposit starts with 500 Ice.

## Resource identity and hardcoded references

The live simulation currently uses `ResourceType.Food`, `ResourceType.Ice`, and `ResourceType.Water` in `InventoryComponent`, `ResourceDeposit`, `FreightDemand`, `TransportContract`, `ContractManager`, `LogisticsManager`, `FarmController`, `CommandPostController`, `WaterProcessorController`, `ShuttleController`, and `MiningShipController`. These are prototype identity branches that T01/T02 will migrate to `ResourceDefinition` assets.

`ResourceType` is referenced by every inventory lookup and mutation, by the deposit's resource field, by freight demand/supply and transport contract data, and by the current controller observability fields. Display/log text also names Food, Ice, and Water. The scene serializes enum ordinals in inventory/deposit/contract-compatible fields and will need explicit asset references during T02; ordinal migration is not safe.

## Bundled responsibilities to separate

- `FarmController`: commute scheduling, passenger work, farmer activity state, staffing count, Water demand, Water consumption, Food production, production blockage, and observability.
- `WaterProcessorController`: conversion algorithm, Water supply publication, operational/input/output state, and observability.
- `CommandPostController`: resident counting, Food consumption, Water consumption, two shortage state machines, Food direct-source resupply, Water demand, and observability.
- `ShuttleController`: ship state, pilot availability, movement, passenger carrier, cargo capacity, freight execution, passenger execution, contract lifecycle, and observability.
- `MiningShipController`: ship state, pilot check, movement, Ice mission planning, deposit extraction, cargo management, unloading, and observability.

## Manual Phase 2 regression capture

The expected current-scene loop is: farmers commute physically to the Farm; the Farm begins work only after arrival, consumes Water, and produces Food; the Command Post consumes Food and Water; the Mining Ship reaches the finite Ice asteroid, extracts and returns Ice to the Processor; the Processor converts Ice to Water; and the Shuttle physically transports freight and farmer passengers. Empty inputs produce starvation/shortage states, while physical delivery changes the receiving inventory only at unload.

The stable baseline assertions are the scene values above and the conservation rules in `InventoryComponent`, `ResourceDeposit`, `ContractManager`, and the two vehicle controllers. The Unity Test Framework tests under `Assets/Tests/EditMode` lock the isolated inventory, finite-deposit, and contract-ordering invariants. A full Play Mode run remains a manual acceptance step because this checkout does not include a headless Unity Editor installation.
