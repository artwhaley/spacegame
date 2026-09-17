# T04 — Recipe Definitions and Generic Resource Converter


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

Create the reusable production primitive that will replace Farm-specific and WaterProcessor-specific conversion logic.

A `ResourceConverterComponent` may offer one or more `RecipeDefinition` assets and execute one selected recipe.

Do **not** migrate Farm or Water Processor in this ticket; prove the primitive independently first.

## `RecipeDefinition`

Create ScriptableObject fields:

```text
stableId
displayName
description

executionMode: Continuous | Batch
durationHours

inputs:  List<ResourceAmount>
outputs: List<ResourceAmount>

staffingRule: RecipeStaffingRule
```

At least one output is required.
Duration must be > 0.

## Validation rules

### Continuous recipes

A Continuous recipe may reference **only Fractional resources** in all inputs and outputs.

Example legal:

```text
4 Ice -> 1 Water over 2h
```

if both Ice and Water are Fractional.

Example illegal:

```text
Metal -> 1 Wrench over 5h
```

when Wrench is Discrete.

The asset must report invalid authoring and the converter must refuse to run an invalid recipe.

### Batch recipes

Batch may reference Fractional and/or Discrete resources.

Any `ResourceAmount` referring to a Discrete resource must be a whole number.

Example:

```text
2.5 Metal + 0.2 Lubricant -> 1 Wrench
```

is legal if Metal/Lubricant are Fractional and Wrench is Discrete.

```text
-> 0.2 Wrench
```

is illegal.

## `ResourceConverterComponent`

References:

```text
InventoryComponent inventory
StaffingComponent staffing (optional)
List<RecipeDefinition> availableRecipes
RecipeDefinition activeRecipe
bool operationalEnabled
```

Expose runtime:

```text
state
progress
throughputMultiplier
blockedReason
```

States may be a small enum such as:

```text
Idle
Running
InputBlocked
OutputBlocked
StaffingBlocked
CompletedBatchWaitingForOutput
Disabled
```

Do not build a generic state-machine framework.

## Continuous execution

For each tick:

1. Verify recipe valid.
2. Verify staffing requirement.
3. Compute effective recipe progress from `deltaGameHours / durationHours * throughputMultiplier`.
4. Limit progress by available inputs.
5. Limit progress by output free capacity.
6. Remove proportional inputs.
7. Add proportional outputs.

All quantity mutations pass Inventory quantity rules.

No partial work is stored beyond what has already been converted.

## Batch execution

Required semantics:

### Starting a batch

Only start when:

- valid recipe;
- required staffing exists;
- all complete batch inputs exist;
- destination inventory has enough free capacity for the complete batch outputs at start.

Atomically consume complete batch inputs at batch start.

Store internal active-batch progress.

### During batch

Advance progress with staffing throughput.

**Do not emit any output before the batch completes.**

A five-hour Wrench recipe therefore shows:

```text
Hour 1: 0 Wrench
Hour 2: 0 Wrench
...
Hour 5: +1 Wrench
```

### Completion

At completion, add the complete outputs atomically.

If output capacity was unexpectedly consumed by another system after the batch began:

- do not delete output;
- keep batch in `CompletedBatchWaitingForOutput`;
- emit outputs only when the full batch fits.

Do not split a discrete output.

Then begin another batch only if inputs/staffing/capacity permit.

## Multiple recipes

The component can list several recipes but runs **one active recipe at a time**.

Provide a simple public method / Inspector reference to choose active recipe.

No automatic recipe switching or production queue in this refactor.

## Tests

Create pure/EditMode or controlled PlayMode tests for:

1. Continuous fractional conversion can produce/consume `0.2`.
2. Continuous recipe referencing Discrete Wrench is invalid/refused.
3. Batch recipe may consume fractional inputs.
4. Batch recipe never exposes fractional Wrench.
5. Batch emits exactly one Wrench only on completion.
6. Batch waits safely if output becomes blocked at completion.
7. Required class blocks production when absent.
8. Additional eligible worker increases throughput by configured amount.
9. Preferred skill adds bonus but cannot substitute for required class.
10. Converter never exceeds inventory capacity or drives inputs negative.

## Guardrails

- No production queues.
- No automatic recipe selection.
- No power/maintenance/quality.
- No item GameObjects.
- No Farm/Processor migration yet.
- No global ProductionManager.

## Acceptance criteria

- Generic converter passes all tests.
- Fractional and discrete production are both natively supported.
- No path can create a fractional Discrete output.
- Existing Phase 2 scene remains unchanged because it is not migrated yet.
