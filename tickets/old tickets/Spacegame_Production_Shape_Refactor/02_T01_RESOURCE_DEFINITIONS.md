# T01 — Resource Definitions and Quantity Semantics


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

Introduce data-driven resource identity and explicit fractional/discrete quantity semantics **without migrating current gameplay yet**.

This ticket creates the new content primitives. T02 performs the atomic migration away from `ResourceType`.

## Create

Suggested paths:

```text
Assets/Scripts/ColonyPrototype/Content/ResourceDefinition.cs
Assets/Scripts/ColonyPrototype/Economy/ResourceAmount.cs
Assets/Scripts/ColonyPrototype/Economy/ResourceQuantityRules.cs
```

Use existing project folder conventions if they have changed.

## `ResourceDefinition`

Create a ScriptableObject with:

```text
stableId       string
displayName    string
description    text
quantityMode   Fractional | Discrete
```

Requirements:

- `stableId` is content identity intended for future saves/data references. It must be non-empty and unique among committed game resources.
- Do not derive gameplay identity from display name.
- Do not add mass, icon, price, rarity, categories, etc. yet.

Create enum:

```text
ResourceQuantityMode
    Fractional
    Discrete
```

## `ResourceAmount`

Serializable structure:

```text
ResourceDefinition resource
float amount
```

Continue using `float` for simulation quantities during this refactor to avoid an unrelated numeric rewrite.

## Quantity rules

Centralize validation in a small pure utility.

Required rules:

### Fractional

Any finite amount >= 0 is legal.

### Discrete

Legal runtime quantities must be whole units within a small epsilon.

Examples:

```text
0        legal
1        legal
12       legal
1.000001 acceptable as 1 within epsilon
0.2      illegal
3.5      illegal
```

Do **not** silently floor `3.9 Wrench` to `3`.

At runtime, mutation APIs receiving a materially fractional discrete amount must reject the operation (return failure/zero as appropriate) and emit a clear development error.

Inspector/editor validation may snap an accidentally-entered near-integer to the nearest whole number, but must not conceal a materially fractional authoring error.

## Create current game assets

Create:

```text
Assets/GameData/Resources/Food.asset
Assets/GameData/Resources/Ice.asset
Assets/GameData/Resources/Water.asset
```

For current prototype parity configure all three as `Fractional`.

Also create an **EditMode test-only** Discrete resource instance or test asset named conceptually `Wrench` to verify semantics. Do not add Wrench to current gameplay.

Suggested stable IDs:

```text
food
ice
water
```

## Tests

Add EditMode tests:

- Fractional accepts `0.2`.
- Discrete accepts `2`.
- Discrete rejects `0.2`.
- Discrete recognizes near-integer epsilon safely.
- invalid negative/non-finite amounts are rejected.
- ResourceAmount authoring validation identifies a fractional amount paired with a Discrete resource.

## Guardrails

- Do not change `InventoryComponent` yet.
- Do not delete or alter `ResourceType` yet except compile-independent helper references.
- Do not create item stacks or a separate discrete inventory.
- Do not add recipes yet.
- Do not add item GameObjects.
- Do not build save/load.

## Acceptance criteria

- Project compiles with both old `ResourceType` and new definitions temporarily present.
- Food/Ice/Water assets exist.
- Current Phase 2 gameplay is unchanged.
- New quantity-rule tests pass.
- The codebase now has one explicit, testable definition of "fractional vs discrete."
