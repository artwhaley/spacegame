# Production-Shaped Architecture

The live simulation is composed from small MonoBehaviours and ScriptableObject content. The current playable scene is `Assets/SpaceSim.unity`; its runtime namespace is `AsteroidColony`.

## Simulation tick

`SimulationManager` owns game time, tick order, and the `ISimulationTickable` registry. A component registers in `Start` and unregisters in `OnDisable`. Facilities, schedules, stock policies, logistics, and extraction therefore advance from simulation time rather than `Update` or wall-clock behavior.

## Resources, inventory, and conservation

`ResourceDefinition` is the identity for a resource. Its `quantityMode` is either `Fractional` or `Discrete`. `ResourceAmount` is the reusable resource/quantity pair used by recipes and other data.

`InventoryComponent` is the local authority for `onHand`, `reserved`, `capacity`, and derived available stock. `Reserve` prevents double-spending; `WithdrawReserved` represents physical pickup; `Add` and `Remove` return actual mutation amounts. Discrete quantities are normalized at every mutation boundary, so fractional discrete stock cannot enter an inventory.

## Recipes and conversion

`RecipeDefinition` is content, not code. Continuous recipes may use fractional resources and apply proportional input/output progress. Batch recipes consume complete inputs at batch start and emit complete outputs only at completion. `ResourceConverterComponent` handles staffing, input/output capacity, blocked states, and local inventory mutation. It never transports outputs.

## Classes, skills, staffing, and schedules

`WorkerClassDefinition` describes hard eligibility such as Pilot or Farm Technician. `SkillDefinition` and `SkillRating` describe proficiency and optional throughput bonuses. A colonist may have multiple classes and skills; a skill never substitutes for a required class.

`StaffingComponent` owns assigned workers and reports who is present, working, and eligible. `WorkScheduleComponent` owns the current two-location commute/work/rest cycle and passenger requests. It does not produce resources or choose vehicles.

## Stock policy and demand

`ResourceStockPolicyComponent` publishes generic import and export policy for an inventory. A foreground policy activates at its reorder threshold and requests its target stock. A background policy can fill spare capacity when no foreground need is active. Export policy exposes only stock above `retainStock`.

Policies identify resources and thresholds; they do not identify a farm, processor, or ship. `LogisticsManager` chooses an eligible source from live `FreightSupply` records.

## Unified dispatch

`ContractManager` owns passenger and freight contract history. `LogisticsManager` arbitrates both open passenger contracts and uncovered freight demand in one path for each idle `TransportVehicleComponent`.

Transport priorities are integers from 1 through 10. Foreground work beats background work, then higher priority wins, then equal-priority vehicle disposition preference, then oldest request, then stable ID. Freight stock is reserved and a freight contract is materialized only after a vehicle candidate wins. This keeps demand publication separate from physical commitment and prevents tiny waiting contracts.

Vehicle dispositions are `Neutral`, `FreightOnly`, `PersonnelOnly`, `PreferFreight`, and `PreferPersonnel`. Preferences apply only at equal priority; hard filters always apply.

## Ship composition

A transport Shuttle is composed from:

- `LocationAnchor` for its logical position;
- `InventoryComponent` for cargo capacity and cargo ownership;
- `ShipComponent` for operational state and qualified Pilot crew;
- `ShipMovementComponent` for direct simulation-time movement;
- `PassengerCarrierComponent` for physical boarding and unboarding;
- `TransportVehicleComponent` for logistics eligibility and disposition;
- `TransportExecutorComponent` for one contract's load/travel/unload execution.

No separate per-resource cargo fields are authoritative. The cargo inventory is the capacity source of truth.

## Extraction is separate

The Mining Ship uses `ShipComponent`, `ShipMovementComponent`, `ResourceCollectorComponent`, and `ExtractionMissionController`. The collector moves resource from a finite `ResourceDeposit` into ship inventory; the mission controller handles destination pressure, travel, unloading, and retry when the destination is blocked.

Extraction does not create `TransportContract` records and does not enter ordinary freight arbitration. This keeps finite-source extraction semantics distinct from warehouse-to-facility freight while still reusing the same inventory, ship, movement, resource, and class primitives.

## Current scene composition

The scene uses generic composition for the Command Post, Farm, Water Processor, Shuttle, and Mining Ship. Content assets under `Assets/GameData` provide the current Food, Ice, Water, class, skill, and recipe definitions. Changing an active recipe, stock threshold, priority, class, skill, or transport disposition is an Inspector/data edit.
