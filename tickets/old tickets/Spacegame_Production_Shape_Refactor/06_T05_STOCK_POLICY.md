# T05 — Reusable Stock Policy, Supply, Demand, and Background Pressure Relief


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

Move "what should this inventory import, retain, export, or opportunistically store?" into a reusable `ResourceStockPolicyComponent`.

Production code should create resources in local Inventory and stop caring who will take them away.

This ticket creates the seam for later inventory blockage, warehouses, and idle-time surplus redistribution without attempting a full economic optimizer.

## Create `ResourceStockPolicyComponent`

Attach alongside:

```text
LocationAnchor
InventoryComponent
```

It contains a list of `ResourceStockPolicyEntry`.

Each entry:

```text
ResourceDefinition resource

// foreground import
bool normalDemandEnabled
float reorderThreshold
float targetStock
int priority                  // 1..10
float minimumShipment
float maximumShipment

// background import / buffer
bool backgroundFillEnabled
float backgroundTargetStock
int backgroundPriority        // 1..10

// export
bool exportEnabled
float retainStock
```

Validate all stock amounts using resource quantity semantics.

For Discrete resources, thresholds/targets/min/max/retain must be whole quantities.

## Foreground demand semantics

Foreground demand becomes active when effective local stock is at/below the configured reorder threshold.

Use:

```text
effectiveStock = onHand + activeInboundForThisPolicy
desired = targetStock - effectiveStock
```

Do not double-count incoming cargo as OnHand.

Stop foreground demand when covered by OnHand + inbound.

## Background demand semantics

If enabled and:

```text
onHand + inbound < backgroundTargetStock
```

publish an opportunistic Background demand for the gap.

Background demand is tagged distinctly from foreground.

It is **not** intended to starve real work. T10 will ensure any foreground transport work beats background work.

This is the future warehouse/buffer seam.

## Export semantics

Exportable amount:

```text
max(0, Inventory.Available(resource) - retainStock)
```

Production controllers do not manually register themselves as suppliers.

Stock policy is the component responsible for making inventory exportable to freight planning.

A future warehouse can both:

- background-fill toward a buffer target;
- export anything above retainStock.

## Integration bridge

The existing `LogisticsManager` currently understands `FreightDemand` and `FreightSupply`.

Refactor those runtime records as needed so a StockPolicy entry publishes/upgrades:

- one foreground demand identity;
- one optional background demand identity;
- one export supply identity.

Do not yet rewrite dispatch arbitration; T10 does that.

`FreightDemand` must include a demand class:

```text
Foreground
Background
```

Keep stable IDs so inbound contract accounting remains associated with the originating demand.

`FreightSupply` must account for `retainStock`, not simply all Inventory.Available.

## No controller-owned source links

After a facility is migrated in later tickets, it must not contain fields like:

```text
foodSourceFarm
waterSourceProcessor
```

The logistics layer chooses among eligible supply nodes.

This ticket may keep old source-specific controller behavior temporarily for compatibility until each facility migration ticket.

## Pressure / blockage behavior supported by this design

This architecture must make the following possible without new producer code:

```text
Water Processor output inventory fills
→ ResourceConverter becomes OutputBlocked

but if a spare-capacity consumer/warehouse has backgroundFill enabled:
→ background freight exists when foreground work is absent
→ Water can be moved out
→ Processor gains output capacity
→ production resumes
```

Similarly:

```text
Water Processor Ice input is full
→ ExtractionMission sees no useful destination capacity
→ miner stops launching
```

Do not implement a more advanced push/pull pressure graph yet.

## Tests

- foreground demand activates only at threshold;
- inbound quantity reduces uncovered demand without increasing OnHand;
- background demand exists below background target;
- foreground and background identities do not double-count inbound;
- export never offers retainStock;
- Discrete stock policies reject fractional thresholds/shipments;
- no supplier may export reserved stock;
- current supply selection can use stock-policy export records.

## Guardrails

- No warehouse building/content yet.
- No automatic global balancing.
- No producer-to-consumer direct references in the new component.
- No arbitrary graph solver.
- No construction.

## Acceptance criteria

- One reusable component can describe Command Post/Farm/Processor-style stock behavior.
- Producers can be ignorant of logistics.
- Exportable stock respects retainStock and reservations.
- Background buffer demand exists as a distinct low-urgency concept.
- Current Phase 2 loop still works through compatibility paths.
