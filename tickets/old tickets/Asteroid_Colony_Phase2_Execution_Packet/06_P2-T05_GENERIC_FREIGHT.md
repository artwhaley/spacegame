# P2-T05 — Make Existing Shuttle Freight Resource-Generic

## Objective

Ensure the proven Phase 1 Shuttle freight system can carry Water using the same path already used for Food.

Do not create a second freight implementation.

---

## Required Context

Phase 1 already has:

```text
Food freight contract
Farm → Shuttle → Command Post
```

Phase 2 requires:

```text
Water freight contract
Water Processor → Shuttle → Farm
```

and:

```text
Water Processor → Shuttle → Command Post
```

The Shuttle should not contain Water-specific behavior.

---

## Tasks

### 1. Inspect the existing freight path

Trace:

```text
Contract creation
→ reservation
→ LogisticsManager assignment
→ Shuttle travel to source
→ Shuttle load
→ Shuttle travel to destination
→ Shuttle unload
→ contract completion
```

Identify any assumptions that freight is always Food.

### 2. Remove only Food-specific assumptions

Freight execution must operate on the contract's:

```text
ResourceType
Quantity
Source Inventory
Destination Inventory
```

Loading:

```text
Source reserved stock decreases
Shuttle same resource increases
```

Unloading:

```text
Shuttle same resource decreases
Destination same resource increases
```

### 3. Preserve reservation behavior

Creating a Water freight contract must reserve Water at its source exactly as Food is reserved today.

Example:

```text
Processor Water:
OnHand = 10
Reserved = 6
Available = 4
```

A second request must only see 4 available.

### 4. Preserve one-contract-at-a-time behavior

Do not add mixed loads or batching.

Shuttle 1 continues to execute one TransportContract at a time.

### 5. Add a simple runtime/manual Water test

Create or temporarily provoke a Water freight contract and confirm the entire transport path works.

Do not leave test-only production code behind unless the project already has a formal test harness.

---

## Expected Files

Likely modifications:

```text
TransportContract.cs
ContractManager.cs
LogisticsManager.cs
ShuttleController.cs
InventoryComponent.cs
```

Only modify files where actual Food hardcoding exists.

---

## Guardrails

Do not:

- create a WaterTransportContract subtype;
- create a second LogisticsManager;
- add multi-resource cargo manifests;
- combine contracts;
- add route planning;
- add multi-stop runs;
- optimize fleet utilization;
- change farmer passenger logic;
- change mining logic.

This ticket makes **existing freight generic**, nothing more.

---

## Acceptance Criteria

Create a Water contract:

```text
Resource: Water
Quantity: 8
Source: Water Processor
Destination: Farm
```

Expected:

1. 8 Water becomes reserved at Processor.
2. Shuttle travels to Processor if needed.
3. 8 Water moves from Processor inventory to Shuttle inventory.
4. Processor no longer owns those 8.
5. Shuttle physically travels.
6. 8 Water moves from Shuttle inventory to Farm.
7. Shuttle returns to zero Water cargo.
8. Contract completes.

Also verify:

- no duplicated Water;
- no negative quantities;
- destination does not receive Water before unloading;
- existing Food freight still works;
- passenger transport still works.
