# T10 — Unified Passenger/Freight Dispatch, Priority 1–10, and Vehicle Disposition


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

Make priority a globally coherent first-order gameplay property.

An idle transport vehicle must choose among **both** passenger and freight work in one arbitration step.

Remove the current architectural split where freight demand may create/assign a contract before open passenger contracts are compared.

## Priority rules

All transport priorities are integer 1..10.

Create one central clamp/validation helper.

Migrate current prototype priorities preserving relative ordering. A reasonable parity mapping is:

```text
farmer passenger commute    10
Command Post Water           9
Farm Water                   6
Food freight                 5
```

But inspect actual scene values and preserve intended ordering.

No runtime priority 50/60/90/100 remains after this ticket.

## Dispatch candidate

Create a small internal/plain runtime representation such as:

```text
TransportDispatchCandidate
```

It is **not** a universal WorkOrder.

It represents one of:

```text
existing open Passenger contract
uncovered Foreground Freight demand
uncovered Background Freight demand
```

For freight, do not reserve/create the contract until a vehicle candidate has won dispatch.

This prevents a freight demand from preemptively consuming an idle vehicle before passenger work is compared.

## Arbitration order per idle vehicle

### Step 1: Eligibility

Filter by:

- vehicle operational/crewed/idle;
- passenger capacity or freight cargo capacity;
- source/destination validity;
- source stock / destination capacity;
- vehicle disposition hard filters.

`FreightOnly` rejects passenger candidates.
`PersonnelOnly` rejects freight candidates.

### Step 2: Foreground before background

If any eligible Foreground candidate exists, ignore all Background candidates.

Passenger work is Foreground.
Normal freight demand is Foreground.
Background stock fill is Background.

### Step 3: Higher priority

Choose highest integer priority 1..10.

### Step 4: Disposition preference at equal priority only

For equal-priority candidates:

```text
PreferFreight    -> prefer freight
PreferPersonnel  -> prefer passenger
Neutral          -> no category preference
```

Preference must **not** allow priority 2 preferred Freight to beat priority 9 Passenger.

### Step 5: Age / deterministic tie-break

Choose oldest request.

For passenger contracts, use contract creation order/time.
For Freight demands, track the tick/time when that demand became active; do not reset its age every tick while still active.

Then stable ID as final deterministic tie-break.

## Freight materialization

Once a Freight candidate wins for a specific vehicle:

1. compute legal shipment amount using uncovered demand, source exportable stock, destination free capacity, max shipment, vehicle capacity;
2. normalize to whole units for Discrete resources;
3. enforce minimum shipment;
4. reserve source;
5. create Freight `TransportContract`;
6. assign to winning `TransportVehicleComponent`.

No waiting micro-contract should be created when no compatible transport is available.

## Contract vehicle type migration

Change:

```text
TransportContract.assignedShuttle
```

to a generic transport vehicle reference, e.g.:

```text
TransportVehicleComponent assignedVehicle
```

Update ContractManager assignment APIs accordingly.

Delete the Shuttle-only assumptions from LogisticsManager.

Remove the temporary Shuttle adapter if no longer needed.

## Player-changing priority

Stock-policy priorities are mutable at runtime.

Changing priority affects **unassigned/current demand and future dispatch**.

Do not reroute a vehicle already executing a contract solely because the number changed.

Passenger priority on WorkSchedule is likewise 1..10.

No custom game UI is required yet; Inspector editing is sufficient.

## Tests

Must include:

1. priority 10 passenger beats priority 9 freight on Neutral.
2. priority 10 freight beats priority 9 passenger.
3. at equal priority, PreferFreight chooses freight.
4. at equal priority, PreferPersonnel chooses passenger.
5. at unequal priority, preference does not override priority.
6. FreightOnly never receives passenger.
7. PersonnelOnly never receives freight.
8. any Foreground candidate beats any Background candidate.
9. oldest equal candidate wins.
10. Discrete freight quantity is whole.
11. source reservation occurs only after a freight candidate actually wins dispatch.
12. changing a demand priority changes the next assignment without affecting an in-flight contract.

## Guardrails

- No route optimization.
- No multi-stop loads.
- No contract preemption.
- No vehicle AI beyond dispatch choice.
- No user-facing UI beyond Inspector.

## Acceptance criteria

- One arbitration path chooses all ordinary Shuttle work.
- Priority scale is universally 1..10.
- Disposition works exactly as specified.
- Background freight only uses otherwise-unused transport opportunity.
- Phase 2 passenger and freight loops still work.
