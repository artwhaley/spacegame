# Spacegame Production-Shape Refactor — Execution Packet


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


## Why this packet exists

The prototype has successfully discovered enough real duplication to justify abstraction. This packet converts the codebase from "Farm knows farming, WaterProcessor knows water processing, MiningShip knows Ice mining" into reusable production primitives without changing the current playable economic loop.

The target philosophy is:

> **ScriptableObjects describe content and rules. MonoBehaviours own live instance state/capabilities. Managers coordinate cross-entity systems.**

Examples:

```text
Farm
├── LocationAnchor
├── InventoryComponent
├── StaffingComponent
├── WorkScheduleComponent
├── ResourceConverterComponent
└── ResourceStockPolicyComponent
```

```text
Water Processor
├── LocationAnchor
├── InventoryComponent
├── ResourceConverterComponent
└── ResourceStockPolicyComponent
```

```text
Shuttle
├── LocationAnchor
├── InventoryComponent
├── ShipComponent
├── ShipMovementComponent
├── PassengerCarrierComponent
├── TransportVehicleComponent
└── TransportExecutorComponent
```

```text
Mining Ship
├── LocationAnchor
├── InventoryComponent
├── ShipComponent
├── ShipMovementComponent
├── ResourceCollectorComponent
└── ExtractionMissionController
```

## Non-negotiable design decisions

### Fractional vs discrete resources

Every `ResourceDefinition` declares one quantity mode:

- `Fractional` — arbitrary positive fractional quantities are legal.
- `Discrete` — inventory, reservations, freight, deposits, collection, and recipe amounts must be whole units.

Do **not** create a second inventory system for discrete items.

Examples:

```text
Water = Fractional
Ice = Fractional
Food = Fractional (for current prototype)
Wrench = Discrete
```

### Recipe execution modes

Recipes support:

- `Continuous`: proportional input/output every simulation tick. **All referenced resources must be Fractional.**
- `Batch`: progresses over time and emits complete outputs only when a batch finishes. May use Fractional and/or Discrete resources. Any amount referring to a Discrete resource must be a whole number.

Thus a 5-hour recipe that produces one Wrench never produces `0.2 Wrench/hour`.

### Priority

Player-facing priority is always an integer **1 through 10**:

- 1 = lowest foreground priority
- 10 = highest foreground priority

Clamp/reject all out-of-range values at system boundaries.

### Transport disposition

A transport-capable vehicle has one setting:

- `Neutral`
- `FreightOnly`
- `PersonnelOnly`
- `PreferFreight`
- `PreferPersonnel`

Initial semantics:

1. `FreightOnly` / `PersonnelOnly` are hard eligibility filters.
2. Priority remains more important than preference.
3. At equal priority, a preference breaks the tie in favor of the preferred transport type.
4. Neutral has no type tie-break.
5. Oldest eligible request wins after priority/preference tie-breaks.

Do not invent more complex weighting until playtesting requires it.

### Foreground vs background logistics

Foreground demand is a real current need.

Background demand exists so otherwise-idle logistics can move useful surplus into spare capacity / buffer storage.

**Any eligible foreground work beats all background freight**, regardless of numeric background priority.

This is the seam for later warehouse depots and pressure relief. Do not build a full balancing optimizer yet.

### Qualifications

Support **both** systems from the start:

- **Class** = hard job eligibility. A colonist may hold multiple classes.
- **Skill** = numeric proficiency used for bonuses; it does not by itself grant job eligibility.

Example:

```text
Colonist classes:
    Pilot
    Farm Technician

Skills:
    Piloting 0.75
    Agriculture 0.45
```

A job requiring class `Pilot` may only use someone with that class.

### Extraction

Extraction remains separate from normal freight. `ExtractionMissionController` may observe destination need/capacity, command a resource collector, and return cargo, but it is not converted into `TransportContract` or a generic WorkOrder.

---

# Execution order

Execute tickets exactly in this order:

1. `01_T00_CHARACTERIZE_AND_LOCK.md`
2. `02_T01_RESOURCE_DEFINITIONS.md`
3. `03_T02_RESOURCE_IDENTITY_MIGRATION.md`
4. `04_T03_CLASSES_SKILLS_STAFFING.md`
5. `05_T04_RECIPE_AND_CONVERTER.md`
6. `06_T05_STOCK_POLICY.md`
7. `07_T06_FARM_COMPOSITION.md`
8. `08_T07_WATER_PROCESSOR_COMPOSITION.md`
9. `09_T08_COMMAND_POST_COMPOSITION.md`
10. `10_T09_SHIP_TRANSPORT_PRIMITIVES.md`
11. `11_T10_UNIFIED_DISPATCH.md`
12. `12_T11_EXTRACTION_COMPOSITION.md`
13. `13_T12_SCENE_CONTENT_MIGRATION.md`
14. `14_T13_LEGACY_REMOVAL.md`
15. `15_T14_FINAL_ACCEPTANCE.md`

## Executor rules

For every ticket:

- Read this file and the ticket completely before editing.
- Inspect the actual current file before changing it.
- Do not assume a path/class still exists merely because this packet names it.
- Modify existing equivalents rather than creating duplicates.
- Keep commits ticket-scoped.
- Compile after every ticket.
- Run specified EditMode/PlayMode tests after every ticket.
- Run the Phase 2 smoke loop after any ticket touching Inventory, Contracts, Logistics, production, people, or vehicles.
- Never solve a future ticket early unless required for compilation; if a temporary compatibility adapter is required, mark it clearly and remove it by T13.
- Do not leave two authoritative representations of the same concept after its atomic migration ticket.

## Final success standard

After T14:

- adding a new resource requires creating a `ResourceDefinition` asset, not editing an enum;
- a discrete Wrench can never become `0.2 Wrench`;
- adding a new conversion facility requires composition + recipe data, not a new building-specific production controller;
- stock/import/export policy and priority are reusable data on facilities;
- priority is globally coherent across freight and passenger work;
- transport disposition is a vehicle setting;
- Shuttle is composition, not a monolithic Shuttle controller;
- Mining Ship uses generic ship movement + a first-class extraction capability;
- a colonist may be multi-class and also carry independent skill ratings;
- the current Phase 2 loop still works.
