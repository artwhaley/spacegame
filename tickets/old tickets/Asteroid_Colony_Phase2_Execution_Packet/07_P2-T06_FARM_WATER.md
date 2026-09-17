# P2-T06 — Make Farm Consume Water and Request Resupply

## Objective

Make Water a required physical input to Food production.

The Farm must stop producing Food when it has farmers but no Water, and must request Water from the Water Processor through the existing freight-contract system.

---

## Required Context

Phase 1 Farm behavior:

```text
Farmers physically present and working
        ↓
Food production
```

Phase 2 behavior:

```text
Farmers physically present and working
AND
Water physically stored at Farm
        ↓
Water consumed
Food produced
```

Water comes from:

```text
Water Processor
        ↓
Shuttle 1
        ↓
Farm
```

---

## Modify

Existing:

```text
FarmController.cs
```

Reuse existing `InventoryComponent` and contract APIs.

Do not replace FarmController with a generic ProductionFacility system.

---

## Farm Inventory

Farm now uses:

```text
Water  — input
Food   — output
```

Recommended Water capacity:

```text
16
```

Starting Water for the final Phase 2 scene:

```text
0
```

---

## Production Rule

Preserve the existing Phase 1 farmer presence/work requirement.

Add a Water requirement.

Recommended initial balance:

```text
Consume: 1 Water
Produce: 2 Food
```

over approximately one game-hour equivalent.

Use the existing Phase 1 production timing style.

If the current Farm produces continuously, consume/produce proportionally.

If it uses discrete ticks/cycles, preserve that style.

Do not redesign the production model merely for this ticket.

---

## Blocking Behavior

If farmers are present and working but Water is unavailable:

```text
Food production stops.
```

Farmers remain assigned/present.

Do not send them home early.

Expose an Inspector-readable reason, e.g.:

```text
Production Blocked: No Water
```

or the nearest equivalent consistent with existing code.

When Water later arrives:

```text
Food production resumes automatically.
```

No manual reset.

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
Reorder Threshold: 3
Target Stock: 8
Maximum Order Size: 8
Water Source: Water Processor
Priority: 60
```

These must remain Inspector-tunable.

---

## Incoming Accounting

Before creating a contract, account for existing incoming Water.

Conceptually:

```text
Desired =
    TargetStock
    - OnHand
    - IncomingActiveWaterContracts
```

Cap requested amount by:

```text
Maximum Order Size
Source Available Unreserved Water
Farm free Water capacity
```

Only create a contract when result > 0.

Do not create duplicate Water contracts every tick.

Use the existing ContractManager query capabilities if available.

If no exact helper exists, add the smallest query needed rather than a new demand-management framework.

---

## Reservation

Once the Farm creates the Water freight contract:

```text
Processor Water becomes reserved.
```

Farm does not receive Water until Shuttle physically unloads it.

---

## Files

Primary:

```text
FarmController.cs
```

Potential small modifications:

```text
ContractManager.cs
```

only if a generic "matching active incoming contract" query is missing.

Avoid changes elsewhere.

---

## Guardrails

Do not:

- generalize Farm into a production framework;
- add irrigation simulation;
- add individual colonist thirst at the Farm;
- add farm storage buildings;
- add crop types;
- add worker efficiency;
- add Water carried by farmers;
- add new UI.

This is one new input dependency.

---

## Acceptance Criteria

### A. Water available

With working farmers and Water:

```text
Farm Water decreases
Farm Food increases
```

### B. No Water

With working farmers but Water = 0:

```text
Food production = 0
Farm clearly reports Water-blocked state
```

### C. Resupply

When Water falls below threshold:

```text
Farm creates one appropriate Water contract
Processor Water is reserved
```

No duplicate overlapping request is created each tick.

### D. Physical arrival

Farm Water increases only after:

```text
Processor → Shuttle → Farm
```

### E. Recovery

After Water arrives:

```text
Food production resumes automatically
```

### F. Regression

Farmer commute/rest behavior remains unchanged.
