# P2-T01 — Add Ice and Water to Shared Resource Systems

## Objective

Extend the existing resource representation so the simulation can represent:

```text
Food
Ice
Water
```

without creating resource-specific inventory classes.

---

## Required Context

Phase 1 already has a working generic Inventory and Food freight system.

Phase 2 will require:

```text
Ice:
Ice Asteroid → Mining Ship → Water Processor

Water:
Water Processor → Shuttle → Farm / Command Post
```

The existing inventory system must remain the single source of truth for stored resource quantities.

---

## Tasks

### 1. Extend ResourceType

Modify the existing enum/equivalent so it contains:

```csharp
Food,
Ice,
Water
```

Do not add speculative future resources.

### 2. Verify Inventory supports all three resources

The existing Inventory must independently represent quantities of:

```text
Food
Ice
Water
```

Preserve existing Phase 1 semantics.

Expected invariants:

```text
OnHand >= 0
Reserved >= 0
Reserved <= OnHand
Available = OnHand - Reserved
```

Capacity rules must remain consistent with Phase 1.

If Phase 1 uses per-resource capacities, preserve that.

If Phase 1 uses one total capacity, preserve it unless that implementation literally cannot represent the new resources.

Do not redesign capacity merely for preference.

### 3. Inspect freight model for Food hardcoding

Verify the existing freight contract carries or can carry:

```text
ResourceType
Quantity
Source Inventory
Destination Inventory
```

If core freight behavior contains Phase 1-specific assumptions such as:

```text
ResourceType.Food
```

inside generic load/unload logic, remove only those hardcoded assumptions.

Do not otherwise redesign TransportContract or ContractManager.

### 4. Verify Inspector observability

During Play mode, an Inventory should be capable of showing independent quantities such as:

```text
Food: 10
Ice: 12
Water: 8
```

using the project's existing Inspector model.

---

## Expected Files

Modify only existing equivalents as needed:

```text
ResourceType.cs
InventoryComponent.cs
TransportContract.cs
```

Potentially `ShuttleController.cs` only if a generic freight path is currently hardcoded to Food.

Avoid new files unless required by the actual Phase 1 implementation.

---

## Guardrails

Do not:

- create `FoodInventory`, `WaterInventory`, or `IceInventory`;
- create resource subclasses;
- introduce ScriptableObject resource definitions;
- create production recipes;
- implement mining;
- create the Water Processor;
- change Food balance;
- change farmer behavior;
- change contract scheduling.

This ticket is about shared resource representation only.

---

## Acceptance Criteria

- project compiles;
- existing Phase 1 Food loop still works;
- Inventory can contain Food, Ice, and Water independently;
- reservations remain correct for any resource type;
- core freight representation does not assume Food;
- no new resource-specific inventory class exists;
- no speculative resource framework was introduced.

---

## Regression Check

Run enough of Phase 1 to confirm Food freight still moves:

```text
Farm → Shuttle → Command Post
```

exactly as before.
