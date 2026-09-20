# Architecture Constitution

The canonical copy. `ROADMAP.md` and every packet README reference this file; if they
disagree, this file wins. Any ticket that violates a rule must say so and justify it.

1. **Dependency direction is one-way:** `Content` ← `Runtime` ← `Presentation` ← `UI`.
   Each is its own asmdef. Runtime never references Presentation or UI. Presentation
   never references UI. (Today only `ColonyPrototype.Runtime` and
   `ColonyPrototype.Editor` exist; P0-S creates Presentation, P0-B creates UI.)

2. **Views are read-only projections.** A presenter/view/UI reads simulation state and
   raises commands. It never writes a simulation field. Deleting every view leaves the
   simulation identical. `InventoryRackView` is the template.

3. **Mutations go through narrow validated commands** that return a result enum.
   `Assign() → AssignmentResult` is the template. One method per player intention. No
   universal command bus, no event soup.

4. **One authority per fact.** Inventory owns quantities. `EmploymentAssignment` owns
   employment. `ColonistAgent.currentLocation` owns the last *arrived* location.
   `TransportContract` owns a transport obligation. `ShipVoyageComponent.Flight` owns a
   ship's pose. Nothing caches a second copy that can drift.

5. **Simulation advances only from `SimulationTick(deltaGameHours)`.** `Update` and
   `FixedUpdate` are presentation-only. Register in `OnEnable`, unregister in
   `OnDisable`. Never read `Time.deltaTime` in Runtime.

6. **Cross-cutting influences on a facility are `IFacilityPerformanceProvider`
   channels** (staffing, power, maintenance, morale, hazard). Consumers read
   `IsOperational` and `GetMultiplier(effect)` and never learn *why*.

7. **Registries, never scene scans, on hot paths.** `FindObjectsByType` is allowed only
   in startup/recovery. Follow the `ShipComponent.Ships` static registry pattern
   (register `OnEnable`, unregister `OnDisable`/`OnDestroy`).

8. **A new facility is composition + content assets, never a bespoke controller.** A
   new MonoBehaviour must be reusable by at least two facility types or the behavior
   belongs in content.

9. **Explicit state, no lying booleans.** Phases are enums with one writer
   (`DutyState`, `ShipMovementPhase`, `TransitState`, `VoyagePhase`). "Disabled" means
   paused *and* a visible blocker — never silently automated, never a teleport, never a
   silent fallback.

10. **File ceiling ~400 lines, one responsibility per class.** Split by responsibility;
    keep a facade when callers exist.

11. **All runtime-authoritative state lives in `[SerializeField]` fields** with
    read-only accessors, so save/load in Phase 1 is a serializer, not a rewrite.

12. **Acceptance criteria are observable behavior** — "when the player does X, Y is
    visible within Z game-hours" — plus one focused EditMode test per ticket as a
    design artifact. Running the Unity Test Runner is not a gate; compiling is.

## Applying the constitution in a ticket

- List the rules the ticket touches and how it satisfies each.
- List files the agent **may modify** and **must create**; anything else is a stop-and-
  report.
- Name the forbidden scope explicitly ("no save/load, no power, no morale…").
- State the observable acceptance in the scene at speed.

## Process rules that protect the constitution

- **Scene ownership:** `Assets/SpaceSim.unity` is edited by one packet at a time.
  Preferred remedy (see `GAPS_AND_OPEN_QUESTIONS.md`): split into additive scenes
  (`Managers`, `Base`, `Environment`, `UI`) so packets own disjoint scenes.
- **Commit per ticket** with the ticket ID in the subject.
- **Docs move with code:** a ticket that changes a documented behavior updates
  `ARCHITECTURE.md` / `STAFFING_ARCHITECTURE.md` / `CONTENT_AUTHORING.md` in the same
  commit.
