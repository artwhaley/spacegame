# P2-T07 — Command Post Water Consumption and Resupply

## Objective

Make the Command Post consume Water in parallel with Food and automatically request Water deliveries from the Water Processor.

This introduces the first competition for the same produced resource:

```text
Water Processor
      ↙    ↘
   Farm   Command Post
```

---

## Required Context

The Command Post already:

- represents habitation for 8 colonists;
- consumes Food;
- requests Food when stock drops;
- receives Food through physical Shuttle freight.

Phase 2 adds Water using the same basic stock/reorder pattern.

Intentional simplification:

> All 8 colonists consume Command Post Water regardless of their temporary physical location.

Do not "fix" that abstraction in this phase.

---

## Modify

Existing:

```text
CommandPostController.cs
```

Potentially reuse small shared helper logic if it already exists.

Do not create a generic Needs framework.

---

## Inventory

Command Post inventory now stores:

```text
Food
Water
```

Recommended final scene configuration:

```text
Starting Water: 16
Water Capacity: 40
```

---

## Consumption Configuration

Expose:

```text
Water Consumption Per Resident Per Game Hour
```

Recommended:

```text
0.1 Water / resident / game hour
```

With 8 residents:

```text
0.8 Water / game hour
```

Use the same resident-count convention Phase 1 uses for Food.

---

## Consumption Behavior

Each simulation tick:

```text
RequiredWater =
    ResidentCount
    × WaterConsumptionRate
    × deltaGameHours
```

Remove up to the available quantity.

Never allow Water below zero.

If demand cannot be fully satisfied, enter Water shortage state.

---

## Shortage State

Expose:

```text
Water Shortage Active
```

On transition into shortage, log once.

Example:

```text
Command Post Water shortage began
```

When supply later resumes, clear the flag and log once:

```text
Command Post Water shortage ended
```

Do not add health, death, morale, or productivity consequences.

---

## Water Demand Configuration

Expose:

```text
Water Reorder Threshold
Water Target Stock
Water Maximum Order Size
Water Source
Water Resupply Priority
```

Recommended:

```text
Reorder Threshold: 8
Target Stock: 16
Maximum Order Size: 12
Water Source: Water Processor
Priority: 90
```

---

## Demand Calculation

When Water is below threshold and there is no sufficient active incoming Water:

```text
Desired =
    TargetStock
    - OnHand
    - IncomingWater
```

Cap by:

```text
Maximum Order Size
Source available unreserved Water
Destination free Water capacity
```

Create a Water freight contract only if final requested amount > 0.

Do not issue duplicate requests every tick.

---

## Competing Demand

This ticket must preserve reservations so Farm and Command Post cannot both claim the same units of Water.

Example:

```text
Processor OnHand = 10
Command Post reserves 8

Processor Available = 2
```

Farm can request at most 2 until more Water is produced or reservation changes.

---

## Existing Food Behavior

Do not regress:

- Food consumption
- Food reorder
- Food freight
- Food shortage

Food and Water must be tracked independently.

A Water shortage must not change Food inventory directly.

---

## Files

Primary:

```text
CommandPostController.cs
```

Potential tiny shared-query change in:

```text
ContractManager.cs
```

only if required.

---

## Guardrails

Do not:

- add personal thirst;
- add Water carried by colonists;
- add showers/sanitation;
- add health consequences;
- add oxygen;
- create `NeedsManager`;
- combine Food and Water into a generalized life-support system yet;
- add game UI.

---

## Acceptance Criteria

### A. Consumption

Command Post Water steadily decreases according to:

```text
8 residents × configured rate
```

### B. Demand

Below threshold:

```text
Water freight contract is created
```

### C. Physical resupply

Water only reaches Command Post through:

```text
Water Processor → Shuttle → Command Post
```

### D. Shortage

At zero Water with ongoing demand:

```text
WaterShortageActive = true
```

### E. Recovery

After Water physically arrives:

```text
WaterShortageActive = false
```

### F. Independence

Food simulation remains correct and independent.

### G. Reservation contention

Farm and Command Post cannot reserve the same Water simultaneously.
