# T00 — Characterize and Lock Current Behavior


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

Before changing architecture, produce a concise explanation of how the current repository actually works and establish regression coverage for the invariants this refactor must preserve.

This ticket changes **no intended gameplay**.

## Deliverables

Create/update:

```text
ARCHITECTURE_CURRENT.md
Assets/Tests/EditMode/...
Assets/Tests/PlayMode/...     (where practical)
```

If Unity Test Framework is absent, add the normal Unity Test Framework package only; do not add a third-party test framework.

## `ARCHITECTURE_CURRENT.md`

Document, with real current file/class references:

1. How simulation time/ticks advance.
2. How tickables register/unregister.
3. How Inventory tracks OnHand / Reserved / Available / Capacity.
4. How freight reservation and physical loading/unloading work.
5. How passenger contracts work.
6. How `LogisticsManager` currently reconciles `FreightDemand` / `FreightSupply`.
7. Why current freight demand and open passenger contracts are not globally arbitrated in one step.
8. How Farm work/rest/commute works.
9. How Water Processor produces.
10. How Command Post consumes and requests resources.
11. How Shuttle movement/execution works.
12. How Mining Ship decides to extract/return/unload.
13. List all current hardcoded references to `Food`, `Water`, or `Ice`.
14. List all references to `ResourceType`.
15. List responsibilities currently bundled into each named controller.

Keep it concise enough to be read by the project owner.

## Characterization tests

At minimum add tests for behaviors that can be isolated reliably:

### Inventory

- Add respects capacity.
- Reserve cannot exceed Available.
- WithdrawReserved decreases both reservation and on-hand.
- ReleaseReservation restores availability.
- No quantity becomes negative.

### ResourceDeposit

- finite extraction;
- partial final extraction;
- no negative remainder.

### Contract ordering

- higher priority open contract wins;
- equal priority resolves oldest first.

### Freight accounting

Where practical without excessive scene setup:

- reserved source stock is not also Available;
- active inbound quantity is not added to destination OnHand before unload.

### Simulation smoke test

If a stable PlayMode test can be created without rebuilding the whole scene, verify tick registration/time. Otherwise document the manual smoke test and do not build a fragile test harness.

## Manual regression capture

Run the current Phase 2 scene and record expected observations in `ARCHITECTURE_CURRENT.md`:

```text
Farmers commute
Farm produces Food
Command Post consumes Food
Mining Ship extracts finite Ice
Processor creates Water
Shuttle distributes Water
Farm consumes Water to make Food
Command Post consumes Water
```

Record the Inspector tuning values actually present in the scene so later migration can preserve them.

## Guardrails

- No new resource definitions yet.
- No production refactor.
- No logistics refactor.
- No class/skill implementation.
- Do not "fix" architecture in this ticket.
- Fix only a real existing defect that blocks characterization; document it explicitly.

## Acceptance criteria

- Project compiles.
- Existing Phase 2 scene behaves as before.
- `ARCHITECTURE_CURRENT.md` exists and describes the actual code, not the intended spec.
- Tests cover Inventory, Deposit, and contract priority basics.
- Test suite passes.
- A future executor can read the document and trace the current simulation without reverse-engineering it again.
