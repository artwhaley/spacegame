# P2-T09 — Inspector and Simulation-Log Observability

## Objective

Make Phase 2 failures understandable without building custom game UI.

The Unity Inspector and existing simulation event log remain the debugging interface.

---

## Required Context

Phase 2 introduces a causal chain:

```text
Deposit
→ Mining Ship
→ Processor
→ Water freight
→ Farm / Command Post
→ Food + survival consumption
```

If the economy stalls, the developer must be able to determine **where and why** by clicking objects.

Do not solve this with a custom UI.

---

## Ice Asteroid Inspector

Must visibly expose:

```text
Display Name
Resource Type
Starting Quantity
Remaining Quantity
Extraction Enabled
```

A developer should immediately see depletion.

---

## Mining Ship Inspector

Must visibly expose at minimum:

```text
Assigned Pilot
Operational Enabled
State

Target Deposit
Home / Unload Location

Current Mission Target Quantity

Ice cargo
Cargo Capacity

Travel Speed
Mining Rate
```

State should make these distinguishable:

```text
Idle
TravelingToDeposit
Mining
ReturningToProcessor
Unloading
```

---

## Water Processor Inspector

Must visibly expose:

```text
Operational Enabled

Ice OnHand
Ice Available
Ice Capacity
Ice Input Target

Water OnHand
Water Reserved
Water Available
Water Capacity

Currently Processing
Input Starved
Output Blocked

Configured conversion rates
```

---

## Farm Inspector

In addition to existing Phase 1 state, expose:

```text
Water OnHand
Water Reserved if relevant
Water Incoming
Water Target

Water reorder values

Production active/block state
Production blocked reason
```

`Incoming` may be calculated from active contracts rather than persisted in Inventory.

Do not duplicate source-of-truth data just for display.

---

## Command Post Inspector

Expose:

```text
Food state

Water OnHand
Water Incoming
Water consumption rate

Water reorder threshold
Water target
Water maximum order

Water shortage active
```

---

## Shuttle Inspector

Existing Inspector must make a Water job understandable.

For the current contract expose/readably show:

```text
Contract ID
Type
Resource
Quantity
Source
Destination
State
```

Cargo must show Water when Water is aboard.

---

## ContractManager / Managers Inspector

It should be possible to see simultaneous open/active contracts such as:

```text
Passenger: Command Post → Farm
Water: Water Processor → Command Post
Water: Water Processor → Farm
Food: Farm → Command Post
```

Do not build a custom EditorWindow unless one already exists.

---

## Simulation Log

Ensure useful Phase 2 transitions are logged, including appropriate equivalents of:

```text
Mining Ship departed for Ice Asteroid 1
Mining Ship arrived
Mining began
Mining Ship collected X Ice
Mining Ship returned
Mining Ship unloaded X Ice

Water Processor started processing
Water Processor stopped: no Ice
Water Processor stopped: output full
Water Processor resumed

Farm created Water request
Farm stopped production: no Water
Farm resumed production

Command Post created Water request
Command Post Water shortage began
Command Post Water shortage ended
```

Avoid spam.

A repeated per-tick message is a bug in observability.

Prefer logging state transitions.

---

## Files

Modify existing Phase 2 components only as necessary:

```text
ResourceDeposit.cs
MiningShipController.cs
WaterProcessorController.cs
FarmController.cs
CommandPostController.cs
TransportContract.cs
ShuttleController.cs
SimulationLog.cs
```

Do not introduce a new UI subsystem.

---

## Guardrails

Do not:

- create Canvas UI;
- create a production dashboard;
- create graphs;
- create custom inspectors unless absolutely necessary;
- create logging frameworks;
- persist event history to disk;
- add analytics.

Use ordinary serialized fields and the existing log.

---

## Acceptance Criteria

A developer can diagnose each of these by clicking objects:

### Case A
"Why is there no Water?"

Possible observation:

```text
Processor Ice = 0
Mining Ship = TravelingToDeposit
```

### Case B
"Why is the Farm not making Food?"

Possible observation:

```text
Farmers present
Water = 0
Blocked Reason = No Water
```

### Case C
"Why has the Mining Ship stopped?"

Possible observation:

```text
Deposit Remaining = 0
```

or:

```text
Processor Ice >= Target
```

or:

```text
Operational Enabled = false
```

### Case D
"Why isn't the Farm Water arriving?"

Possible observation:

```text
Water contract open
Shuttle currently servicing higher-priority contract
```

No custom UI is required to reach these answers.
