# T07 — Replace Water Processor Controller with Generic Conversion + Stock Policy


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

Replace `WaterProcessorController` with reusable `ResourceConverterComponent` and `ResourceStockPolicyComponent`.

No new worker requirement is introduced in this migration.

## Target composition

```text
Water Processor
├── LocationAnchor
├── InventoryComponent
├── ResourceConverterComponent
└── ResourceStockPolicyComponent
```

## Recipe asset

Create:

```text
IceToWater.asset
```

Configure it to reproduce the current Processor conversion rate.

For current Phase 2, Ice and Water are Fractional. Use `Continuous`.

Set:

```text
staffingRule.minimumWorkers = 0
```

The architecture supports Technician-class staffing and preferred Water Processing skill, but enabling that requirement now would change gameplay. Do not do it in this refactor.

## Stock policy

Configure:

### Ice

The Processor should want Ice up to its current operational target.

Because Extraction remains separate from freight, this StockPolicy entry may expose target/capacity data to `ExtractionMissionController` but should **not** automatically request ordinary freight unless explicitly enabled.

Recommended:

```text
normalDemandEnabled = false for Ice
backgroundFillEnabled = false for Ice
exportEnabled = false for Ice
targetStock = current mining destination target (for extraction observer)
```

If StockPolicy implementation separates "desired stock metadata" from freight enablement, use that cleanly.

### Water

Water output becomes exportable through StockPolicy:

```text
exportEnabled = true
retainStock = current intended retained amount (normally 0)
```

Optionally allow background behavior only if required to exercise the stock-policy feature, but do not change normal Phase 2 balance.

## Output blockage

Generic converter must stop when Water capacity is full.

No Water is deleted.

This is the first migrated proof of inventory pressure:

```text
nobody removes Water
→ Water inventory fills
→ converter reports OutputBlocked
→ Ice consumption stops
```

If background-capacity logistics later moves Water out, converter resumes automatically.

## Remove `WaterProcessorController`

After migration:

- delete it, or leave only a temporary no-logic adapter marked for T13 removal.

No component named WaterProcessor should own the Ice→Water algorithm after this ticket.

## Tests / manual parity

- Ice decreases and Water increases at same effective rate as before;
- zero Ice -> InputBlocked;
- Water full -> OutputBlocked and Ice does not decrease;
- removing Water -> conversion resumes;
- Water export exists through StockPolicy, not direct producer registration;
- Mining Ship current compatibility path can still determine destination need/capacity.

## Guardrails

- Do not assign Technician workers yet.
- Do not add a production queue.
- Do not add multiple processors.
- Do not rewrite extraction here.

## Acceptance criteria

- Processor is a generic converter instance.
- No Water-specific production algorithm exists outside Recipe data/current content.
- Phase 2 water economy still runs.
