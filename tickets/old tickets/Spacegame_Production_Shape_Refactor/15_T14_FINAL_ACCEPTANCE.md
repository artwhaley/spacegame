# T14 — Final Acceptance, No-Code Authoring Proof, and Architecture Documentation


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

Prove that the refactor achieved production shape without changing the playable Phase 2 simulation.

Fix defects only. Do not add new gameplay.

## A. Automated tests

All previous tests must pass.

Add/retain explicit final coverage for:

### Quantity semantics

- Fractional Water may be `0.2`.
- Discrete Wrench may never be `0.2`.
- Inventory rejects fractional Discrete mutations.
- Freight never reserves/ships fractional Discrete quantities.
- Deposit/Collector never produces fractional Discrete stock.

### Recipe semantics

- Continuous fractional recipe produces proportional partial quantities.
- Continuous recipe containing Discrete resource is invalid.
- Batch discrete recipe emits nothing before completion.
- Batch recipe emits whole discrete outputs at completion.
- blocked completed batch preserves output without loss.

### Qualifications

- multi-class colonist is legal.
- required class is hard eligibility.
- skill alone does not grant eligibility.
- skill and extra-worker bonuses are deterministic.

### Stock policy

- foreground threshold/target behavior;
- inbound accounting;
- retainStock export floor;
- background buffer demand;
- foreground beats background.

### Dispatch

- global freight/passenger priority 1..10;
- disposition filters;
- equal-priority preferences;
- priority dominates preference;
- oldest deterministic tie-break.

### Conservation

- production;
- freight;
- extraction;
- unload.

## B. Manual current-game acceptance

Run the real Phase 2 scene.

Verify:

```text
Farmers commute physically
Farm is staffed only after arrival
Farm consumes Water and produces Food
Command Post consumes Food + Water
Mining Ship physically reaches Ice
Ice Deposit depletes
Mining Ship physically returns Ice
Processor converts Ice → Water
Shuttle physically carries Food/Water
Reservations prevent double-spending
```

### Blockage

Force Water Processor Water output full.

Expected:

```text
Processor stops
Ice consumption stops
```

Remove Water via transport/Inspector test.

Expected:

```text
Processor resumes
```

Fill Processor Ice input / remove downstream capacity.

Expected:

```text
ExtractionMission stops launching when no useful destination capacity exists
```

### Priority gameplay

Create simultaneous passenger + freight need.

Change priority values in Inspector.

Expected:

```text
next idle Shuttle assignment changes accordingly
```

Set Shuttle dispositions and verify exact specified behavior.

### Background movement

With no foreground work, configure a background buffer target with spare capacity.

Expected:

```text
exportable surplus may be moved into buffer
```

Introduce any foreground work.

Expected:

```text
foreground wins
```

## C. No-code authoring proof

Without creating a new C# building controller, perform a temporary authoring exercise:

1. Create a temporary Fractional resource definition `TestInput`.
2. Create a temporary Discrete resource `TestWrench`.
3. Create a Batch Recipe:
   ```text
   2 TestInput -> 1 TestWrench
   duration 2h
   ```
4. Compose a temporary GameObject from:
   ```text
   LocationAnchor
   InventoryComponent
   ResourceConverterComponent
   ```
5. Run it.

Verify no Wrench exists before completion and exactly one appears after.

Delete temporary game content after proof unless it is kept under Tests.

This is the acceptance proof that new production content no longer requires a custom `.cs` controller.

## D. Documentation

Replace/augment `ARCHITECTURE_CURRENT.md` with:

```text
ARCHITECTURE.md
CONTENT_AUTHORING.md
```

### `ARCHITECTURE.md`

Explain:

- simulation tick;
- resource definitions and quantity modes;
- inventory/reservation;
- recipe continuous vs batch;
- classes vs skills;
- staffing/work schedules;
- stock policy / foreground/background;
- logistics dispatch and dispositions;
- ship composition;
- extraction subsystem;
- why extraction is separate.

### `CONTENT_AUTHORING.md`

Give concise exact steps for:

1. add a Fractional resource;
2. add a Discrete resource;
3. add a Continuous recipe;
4. add a Batch/discrete recipe;
5. compose a new converter facility;
6. configure import/export/priority;
7. make a warehouse-like buffer using background stock policy;
8. assign classes/skills to a colonist;
9. configure a Shuttle disposition;
10. configure a resource collector/extraction ship.

## E. Final repo assertions

Repo search should demonstrate:

```text
0 ResourceType references
0 FarmController references
0 WaterProcessorController references
0 CommandPostController references
0 ShuttleController references
0 MiningShipController references
0 legacy priorities > 10 in transport gameplay
```

Generic production/logistics algorithms contain no Food/Water/Ice branches.

## Stop condition

When this ticket passes, STOP.

Do not implement Phase 3 automatically.

The project owner should now play the same simulation through the production-shaped architecture and choose the next gameplay slice from observed behavior.

## Acceptance criteria

This refactor is complete only when all automated/manual/no-code authoring/documentation checks pass.
