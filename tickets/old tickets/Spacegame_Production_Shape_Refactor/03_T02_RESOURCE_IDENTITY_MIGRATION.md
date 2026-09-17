# T02 — Atomic Resource Identity Migration


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

Atomically migrate all live simulation code from `ResourceType` enum identity to `ResourceDefinition` references, then delete `ResourceType`.

Do not leave two authoritative resource identity systems after this ticket.

## Scope

Search the entire repo for:

```text
ResourceType
Food
Ice
Water
```

Classify every hit as:

- legitimate display/test/content reference; or
- hardcoded gameplay identity requiring migration.

Expected affected areas include:

```text
InventoryComponent
InventoryEntry
ResourceDeposit
FreightDemand / FreightSupply
TransportContract
ContractManager
LogisticsManager
FarmController
CommandPostController
WaterProcessorController
ShuttleController
MiningShipController
scene serialized references
tests
```

Actual repo is authoritative.

## Inventory migration

Change Inventory entries from:

```text
ResourceType resource
```

to:

```text
ResourceDefinition resource
```

All public APIs take `ResourceDefinition`.

Preserve:

```text
OnHand
Reserved
Available
Capacity
```

### Enforce quantity mode at Inventory boundary

All mutating methods (`Add`, `Remove`, `Reserve`, `ReleaseReservation`, `WithdrawReserved`, and any equivalents) must call the centralized quantity rules.

For Discrete resources:

- `Add(wrench, 0.2)` fails; no mutation.
- `Reserve(wrench, 0.2)` fails.
- `Remove(wrench, 0.2)` fails.
- serialized OnHand/Reserved/Capacity values validate to whole numbers or report authoring errors.

Do not permit the inventory to become the place where fractional discrete corruption begins.

## Freight / contract migration

Replace resource enum fields with `ResourceDefinition`.

A Freight contract for a Discrete resource must have a whole:

```text
quantity
loadedQuantity
deliveredQuantity
```

Contract creation must reject materially fractional Discrete quantities.

Any capacity/min/max shipment calculations must normalize legal quantities for Discrete resources without silently creating fractions.

If a planner computes 3.7 units of available Discrete capacity, it may propose at most 3 whole units; this planner-side floor is allowed because it is choosing a legal shipment size, not mutating an existing quantity.

## Deposit migration

`ResourceDeposit` references `ResourceDefinition`.

Extraction respects quantity semantics:

- Fractional deposit may extract fractional quantities.
- Discrete deposit returns only whole quantities.

No negative stock.

## Scene migration

Wire all current serialized references to the Food/Ice/Water assets from T01.

Do not rely on enum ordinal migration.

## Delete old enum

After all compile references are migrated:

```text
delete ResourceType.cs
```

Run a repo search proving no runtime reference remains.

## Tests

Update T00 tests to definitions.

Add:

- discrete Inventory cannot contain fractional onHand via public runtime API;
- discrete Freight contract cannot be created for 0.2 units;
- discrete Deposit never returns fractional quantities;
- fractional Food/Water/Ice Phase 2 behavior remains legal.

## Guardrails

- No recipe refactor.
- No named controller decomposition.
- No priority change.
- No logistics architecture change beyond type migration.
- Do not introduce strings as resource identity.

## Acceptance criteria

- `ResourceType` no longer exists.
- Repo search finds zero compile-time references to it.
- Phase 2 scene references Food/Ice/Water assets.
- All tests pass.
- Phase 2 manual loop still runs.
- Discrete resource mutation cannot produce `0.2 Wrench`.
