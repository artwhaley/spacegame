# T06 — Replace Farm Production Controller with Composition


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

Remove Farm-specific production and demand logic from `FarmController`.

Rebuild the current Farm from reusable capabilities while preserving its current commute/work/rest and Water→Food behavior.

## Target Farm composition

```text
Farm GameObject
├── LocationAnchor
├── InventoryComponent
├── StaffingComponent
├── WorkScheduleComponent
├── ResourceConverterComponent
└── ResourceStockPolicyComponent
```

After this ticket, no Farm-specific code may be responsible for Water demand or Food production.

## Create `WorkScheduleComponent`

Extract the reusable shift/commute behavior currently bundled in `FarmController`.

Fields:

```text
LocationAnchor workplace
LocationAnchor home
StaffingComponent staffing

float workDurationHours
float restDurationHours
int passengerPriority     // 1..10
```

It owns the existing group cycle:

```text
AwaitingWorkTransport
Working
AwaitingReturnTransport
Resting
```

Responsibilities:

- create passenger contracts for assigned workers when travel is required;
- begin Work only after workers physically arrive;
- begin Rest only after workers physically arrive home;
- set/maintain colonist activity states used by Staffing;
- repeat cycle.

Do not generalize into arbitrary schedules or multiple shifts.

## Farm recipe asset

Create:

```text
HydroponicFood.asset
```

Configure to reproduce current Farm behavior.

Because Food and Water are currently Fractional, use `Continuous`.

Derive duration/input/output values so current Water consumption and Food production rates remain functionally equivalent to current scene tuning.

Staffing rule:

```text
minimumWorkers = current required farmer count
requiredClass = Farm Technician
```

Configure skill bonus = 0 initially unless current gameplay already has such a bonus.

Do not change throughput during migration.

## Farm Staffing

Assign existing two farmer colonists to Farm `StaffingComponent`.

Ensure they have `Farm Technician` class.

`WorkScheduleComponent` references the same assigned workers.

## Stock policy

Replace Farm Water demand fields with a StockPolicy entry for Water preserving current:

```text
reorder threshold
target stock
priority (map to 1..10 preserving relative order)
minimum / maximum shipment
```

Configure Farm Food as exportable supply with an appropriate `retainStock` matching current behavior (usually 0 unless current scene intentionally retains some).

Do not add background Food storage unless needed only for test demonstration.

## Converter

Farm `ResourceConverterComponent`:

```text
inventory = Farm Inventory
staffing = Farm Staffing
activeRecipe = HydroponicFood
```

It operates only while Staffing reports the needed eligible working workers.

## Remove / shrink `FarmController`

Once parity is established:

- delete `FarmController` if all responsibilities have moved;
- if a temporary compatibility adapter is unavoidable, it must contain no production/demand logic and must be scheduled for deletion by T13.

## Tests / manual parity

Verify:

- farmers still commute to Farm;
- production does not begin before physical arrival;
- resting still begins only after physical return home;
- no Water -> Food production stops;
- Water arrives -> production resumes;
- Food output rate matches pre-refactor tuning within epsilon;
- Food is exportable to logistics;
- Farm Water priority is 1..10 and editable in StockPolicy;
- no hardcoded Water/Food logic remains in Farm-specific C#.

## Guardrails

- Do not add crops.
- Do not add multiple recipes to Farm gameplay yet (converter capability may support them).
- Do not add automatic job assignment.
- Do not change passenger scheduling semantics beyond priority normalization.

## Acceptance criteria

- Farm behavior is composition-driven.
- `FarmController` is gone or a zero-logic temporary adapter.
- Current Phase 2 farm loop is preserved.
