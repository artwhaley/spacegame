# P2-T04 — Automated Ice→Water Processor

## Objective

Add a Water Processor facility that stores Ice and Water and automatically converts Ice into Water.

The Processor is intentionally unstaffed in Phase 2.

---

## Required Context

Mining Ship 1 delivers Ice physically into the Water Processor's Inventory.

Downstream Shuttle freight will later move Water from this facility to:

```text
Farm
Command Post
```

Phase 2 is testing the extraction/processing/logistics economy, not labor allocation.

---

## New Component

Create:

```text
WaterProcessorController.cs
```

Attach to a new GameObject:

```text
Water Processor
```

Also attach/reuse:

```text
LocationAnchor
InventoryComponent
```

---

## Inventory Configuration

The Water Processor inventory must support:

```text
Ice
Water
```

Recommended capacities:

```text
Ice Capacity: 48
Water Capacity: 40
```

Use the actual Inventory model established in Phase 1.

---

## Inspector Fields

Expose:

```text
Inventory

Operational Enabled

Ice Input Target

Ice Consumption Rate
Water Production Rate

Currently Processing
Input Starved
Output Blocked
```

Recommended Phase 2 values:

```text
Ice Input Target: 24

Ice Consumption: 2 Ice / game hour
Water Production: 2 Water / game hour
```

The initial recipe is therefore conceptually:

```text
1 Ice → 1 Water
```

---

## Simulation Behavior

Register with the existing simulation tick mechanism.

Process only when all are true:

```text
Operational Enabled
Ice OnHand > 0
Water has free storage capacity
```

Per simulation tick, consume and produce according to the configured rate.

Respect the project's existing quantity type.

If Phase 1 inventories use integral quantities, implement equivalent discrete cycles rather than forcing a floating-point redesign.

---

## Conservation Rule

At the initial 1:1 ratio:

```text
Ice consumed == Water produced
```

Do not remove Ice unless the resulting Water can actually be stored.

If Water storage is full:

```text
Ice consumption = 0
Water production = 0
OutputBlocked = true
```

If Ice is zero:

```text
InputStarved = true
```

---

## Mining Ship Integration

Expose or make available enough read-only information for Mining Ship logic to understand:

```text
Ice On Hand
Ice Input Target
Ice Storage Free Capacity
```

Prefer the existing Inventory API where possible.

Do not create a mining contract.

If P2-T03 temporarily used direct Inspector references for desired Ice stock, wire it to this component only if doing so is a small, obvious improvement.

Avoid unnecessary circular dependencies.

---

## Logging

Log meaningful state changes, not every tick.

Useful events:

```text
Water Processor started processing
Water Processor stopped: no Ice
Water Processor stopped: Water storage full
Water Processor resumed processing
```

Avoid repeated identical log spam.

---

## Files

Create:

```text
WaterProcessorController.cs
```

Modify MiningShipController only if needed for the minimal processor stock-target connection.

---

## Guardrails

Do not:

- assign workers;
- add a Processor profession;
- create ProductionFacility base classes;
- create Recipe ScriptableObjects;
- create a production graph;
- add power;
- add maintenance;
- add efficiency;
- add upgrades;
- add Water contracts yet.

This ticket only converts local Ice to local Water.

---

## Acceptance Criteria

### Case A — Normal processing

Starting:

```text
Ice = 10
Water = 0
```

After sufficient game time:

```text
Ice decreases
Water increases
```

At 1:1, total Ice lost equals Water gained.

### Case B — No Ice

Starting:

```text
Ice = 0
```

Expected:

```text
Water production = 0
InputStarved = true
```

### Case C — Full output

Fill Water to capacity.

Expected:

```text
Ice does not decrease
Water does not exceed capacity
OutputBlocked = true
```

### Case D — Resume

Create output capacity or add Ice again.

Expected:
processing resumes automatically.

### Regression

Phase 1 Food/farmer/Shuttle loop still works.
