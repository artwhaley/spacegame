# P2-T03 — Mining Ship Extraction Loop

## Objective

Give Pilot 2 a dedicated Mining Ship that physically travels to Ice Asteroid 1, extracts finite Ice into its own cargo inventory, returns to the Water Processor location, and unloads that Ice.

Mining is intentionally specialized in Phase 2.

---

## Required Context

Phase 1 has:

```text
Pilot 1 → Shuttle 1
Pilot 2 → currently idle
```

Phase 2 changes Pilot 2 to:

```text
Pilot 2 → Mining Ship 1
```

A finite `ResourceDeposit` from P2-T02 already exists.

A Water Processor controller is implemented in the next ticket. To avoid unnecessary dependency coupling, the Mining Ship should be configurable with:

- target deposit;
- unload/home `LocationAnchor`;
- destination `InventoryComponent`;
- desired destination Ice stock target.

If P2-T04 subsequently provides a cleaner read-only stock-target API, wire to it minimally then.

---

## New Component

Create:

```text
MiningShipController.cs
```

Attach it to a new primitive GameObject:

```text
Mining Ship 1
```

Also attach/reuse:

```text
LocationAnchor
InventoryComponent
```

according to Phase 1 conventions.

---

## State Machine

Use exactly the states currently required:

```text
Idle
TravelingToDeposit
Mining
ReturningToProcessor
Unloading
```

Do not create a generic state-machine framework.

---

## Inspector Fields

Expose approximately:

```text
Display Name
Operational Enabled
Assigned Pilot

Target Deposit
Home / Unload Location
Destination Inventory

Desired Destination Ice Stock

Cargo Capacity
Travel Speed
Mining Rate

State
Current Mission Target Quantity
Current Cargo Quantity
```

Use the project's actual numeric types.

Recommended defaults:

```text
Display Name: Mining Ship 1
Cargo Capacity: 24 Ice
Mining Rate: 6 Ice / game hour
Travel Speed: roughly Shuttle scale
Operational Enabled: true
```

---

## Pilot Initialization

Assign:

```text
Pilot 2
```

to the Mining Ship in the Inspector.

At initialization, make Pilot 2 logically/physically aboard the Mining Ship using the same simple convention Phase 1 uses for Pilot 1 and Shuttle 1.

Do not introduce shift scheduling.

The Mining Ship is not available when:

```text
Operational Enabled == false
```

or there is no Assigned Pilot.

No other crew rule is needed here.

---

## Mining Need Calculation

While Idle, calculate whether the destination needs Ice.

Conceptually:

```text
IceNeeded =
    DesiredDestinationIceStock
    - DestinationIceOnHand
    - IceAlreadyAboardThisShip
```

For Phase 2, no other Mining Ship exists, so no global "incoming mining cargo" accounting is necessary.

If `IceNeeded <= 0`, remain Idle.

If deposit remaining is zero, remain Idle.

If destination has no free Ice capacity, remain Idle.

---

## Mission Quantity

When beginning a trip:

```text
MissionQuantity = minimum of:

IceNeeded
Mining Ship free cargo capacity
Destination free Ice capacity
Deposit remaining
```

Never depart for a zero-quantity mission.

Store this quantity for Inspector/debugging.

---

## Travel To Deposit

Use the existing Phase 1 movement style where possible.

Requirements:

- direct Transform movement;
- simulation-time driven;
- no physics;
- no pathfinding;
- no acceleration simulation.

The ship must visibly move through world space.

On arrival, transition:

```text
TravelingToDeposit → Mining
```

---

## Mining

While Mining:

```text
requestedThisTick =
    MiningRate * deltaGameHours
```

Call the deposit extraction API.

For exactly the amount actually extracted:

```text
Deposit Remaining -= X
Mining Ship Ice += X
```

Cargo is stored in the existing `InventoryComponent`.

Stop mining when any of these is true:

- mission quantity collected;
- ship cargo full;
- deposit depleted;
- destination cannot accept any more Ice.

Do not fabricate a full cargo load if the deposit contains less.

---

## Return

After mining, if ship carries Ice:

```text
Mining → ReturningToProcessor
```

Travel physically to the configured home/unload location.

Ice remains in Mining Ship inventory during the trip.

Do not increment destination inventory early.

---

## Unload

On arrival:

```text
ReturningToProcessor → Unloading
```

Transfer Ice from Mining Ship inventory to Destination Inventory.

If destination cannot accept the full load:

- transfer only what fits;
- keep the remainder aboard;
- remain in Unloading;
- retry on later simulation ticks.

Never destroy excess cargo.

When cargo reaches zero:

```text
Unloading → Idle
```

---

## Operational Disable Behavior

Keep this simple.

Required:

- when Idle and disabled, do not start a new mission.

For an already-running mission, choose the smallest coherent behavior supported by current code:
- either finish the current trip and then remain idle;
- or pause current active work cleanly.

Document the chosen behavior in code comments.

Do not build emergency return logic.

---

## Logging

Use trip-level messages such as:

```text
Mining Ship 1 departing for Ice Asteroid 1
Mining Ship 1 arrived at Ice Asteroid 1
Mining Ship 1 began mining
Mining Ship 1 collected 24 Ice
Mining Ship 1 returning to processor
Mining Ship 1 unloaded 24 Ice
```

Do not log each simulation tick.

---

## Files

Create:

```text
MiningShipController.cs
```

Modify existing shared movement/location helpers only if a tiny reuse change is clearly safer than duplication.

Do not refactor ShuttleController into a vehicle framework in this ticket.

---

## Guardrails

Do not create:

- MiningContract
- WorkOrder
- VehicleTask
- MiningManager
- FleetManager
- generic vehicle inheritance hierarchy
- fuel
- pilot fatigue
- repairs
- mining equipment
- multiple mining targets
- route selection
- scanning

Mining Ship is deliberately specialized.

---

## Acceptance Criteria

With a destination that needs 24 Ice and a deposit containing at least 24:

```text
Mining Ship leaves home
→ reaches Ice Asteroid
→ mines exactly 24
→ asteroid loses exactly 24
→ Mining Ship carries exactly 24
→ returns home
→ destination gains exactly 24
→ Mining Ship returns to 0 cargo
→ ship returns to Idle
```

Additional criteria:

- ship never departs for zero need;
- ship does not mine when disabled;
- ship does not operate without Pilot 2 assigned;
- ship cannot collect more than cargo capacity;
- ship cannot collect more than deposit remaining;
- no Ice teleports;
- Phase 1 still runs.
