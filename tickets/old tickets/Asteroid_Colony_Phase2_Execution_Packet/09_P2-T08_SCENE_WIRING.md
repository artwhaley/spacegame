# P2-T08 — Phase 2 Scene Wiring and Initial Balance

## Objective

Wire the new Phase 2 objects and references into the existing tested scene using deliberately simple initial balance values.

This ticket is scene configuration, not new architecture.

---

## Required Context

By this point the project should have:

- finite ResourceDeposit
- MiningShipController
- WaterProcessorController
- generic Water freight
- Farm Water consumption/demand
- Command Post Water consumption/demand

Existing Phase 1 world remains intact.

---

## Add Scene Objects

Create:

```text
Ice Asteroid 1
Water Processor
Mining Ship 1
```

Use simple Unity primitives.

No final art.

---

## Recommended World Positions

Preserve existing Phase 1 positions if they differ.

A good default layout is:

```text
Command Post      (0, 0, 0)
Farm              (12, 0, 0)
Water Processor   (0, 0, 10)
Ice Asteroid 1    (24, 0, 10)
```

Place:

```text
Mining Ship 1
```

at/near Water Processor.

The exact coordinates are tunable.

The purpose is simply to make travel visible.

---

## Ice Asteroid 1

Configure:

```text
Display Name: Ice Asteroid 1
Resource Type: Ice
Starting Quantity: 500
Remaining Quantity: 500
Extraction Enabled: true
```

---

## Mining Ship 1

Attach/reuse:

```text
LocationAnchor
InventoryComponent
MiningShipController
```

Configure:

```text
Display Name: Mining Ship 1
Assigned Pilot: Pilot 2
Target Deposit: Ice Asteroid 1
Home/Unload Location: Water Processor
Destination Inventory: Water Processor Inventory

Cargo Capacity: 24 Ice
Mining Rate: 6 Ice / game hour
Operational Enabled: true
```

Travel speed should be roughly comparable to Shuttle scale.

Do not tune for perfection.

At simulation start:

```text
Pilot 2 location = Mining Ship 1
Mining Ship starting location = Water Processor
```

---

## Water Processor

Attach:

```text
LocationAnchor
InventoryComponent
WaterProcessorController
```

Configure inventory:

```text
Ice:
Starting = 0
Capacity = 48

Water:
Starting = 0
Capacity = 40
```

Configure Processor:

```text
Ice Input Target: 24
Ice Consumption: 2 / game hour
Water Production: 2 / game hour
Operational Enabled: true
```

Wire Mining Ship desired Ice stock to the Processor's 24 target using the implemented Phase 2 interface.

---

## Farm

Preserve Phase 1 configuration.

Add:

```text
Water Starting: 0
Water Capacity: 16

Water Reorder Threshold: 3
Water Target Stock: 8
Water Max Order Size: 8
Water Source: Water Processor
Water Resupply Priority: 60
```

Production ratio:

```text
1 Water → 2 Food
```

while existing farmer-presence conditions are satisfied.

---

## Command Post

Preserve existing Phase 1 Food settings.

Add:

```text
Water Starting: 16
Water Capacity: 40

Water Consumption: 0.1 / resident / game hour
Water Reorder Threshold: 8
Water Target Stock: 16
Water Max Order Size: 12
Water Source: Water Processor
Water Resupply Priority: 90
```

---

## Shuttle 1

Do not add a second general-purpose Shuttle.

Existing Shuttle 1 must service:

- farmer commute
- Food freight
- Farm Water freight
- Command Post Water freight

Preserve current Phase 1 passenger priorities.

Use the configured Water priorities from Farm/Command Post.

Do not tune away all contention.

If one Shuttle becomes a bottleneck, that is useful Phase 2 evidence.

---

## Pilots

Final Phase 2 assignment:

```text
Pilot 1 → Shuttle 1
Pilot 2 → Mining Ship 1
```

No shift logic.

---

## Managers

Reuse existing Managers GameObject.

Do not add:

- MiningManager
- WaterManager
- ResourceManager

unless one already exists from Phase 1 for an equivalent generic responsibility.

The new controllers should participate through existing simulation tick/registries as appropriate.

---

## Guardrails

Do not:

- change project structure unnecessarily;
- add art;
- add physics;
- add UI;
- add new colonists;
- add a second Shuttle;
- add a second asteroid;
- add a second Water Processor;
- add staffing.

This ticket should mainly be Inspector wiring.

---

## Acceptance Criteria

On entering Play mode:

- no missing-reference errors;
- all 8 colonists register as before;
- Pilot 1 is aboard Shuttle 1;
- Pilot 2 is aboard Mining Ship 1;
- Mining Ship recognizes the Processor needs Ice;
- Mining Ship can travel toward Ice Asteroid 1;
- Water Processor starts with zero Ice/Water;
- Command Post starts with Water;
- Farm starts with no Water;
- Phase 1 farmer loop remains functional;
- the simulation can proceed without manually changing Inspector references after Play begins.
