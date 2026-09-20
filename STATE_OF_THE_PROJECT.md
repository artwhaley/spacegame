# State of the Project (snapshot: 2026-09-18, commit `c2b92cc`)

## Numbers

| Metric | Value |
|---|---|
| Runtime source | ~7,300 lines, 49 files, single `ColonyPrototype.Runtime` asmdef |
| Tests | ~3,500 lines, 23 EditMode files + 1 PlayMode |
| Commits | 22 in 4 days (Sept 14–17) |
| Content assets | 7 classes, 4 effects, 2 recipes, 3 resources, 2 roles, 1 shift pattern, 4 skills |
| Scene | `SpaceSim.unity`: CommandPod, Farm, Water Processor, Shuttle, Mining Ship 1, Ice Asteroid 1, one authored colonist (`Pilot 3`), primitives for visuals |
| Unity | 6000.5.9f1, HDRP 17.5, Input System 1.20, uGUI 2.5 (UI Toolkit modules present) |

## What exists and is good

The simulation backend is complete for its scope and the docs match the code.

- **Content layer** — `ResourceDefinition`, `RecipeDefinition`, `WorkerClassDefinition`,
  `SkillDefinition`, `StaffingRoleDefinition`, `ShiftPatternDefinition`,
  `FacilityEffectDefinition`. New facility = composition + assets; proven by test.
- **Economy** — `InventoryComponent` is sole quantity authority (onHand/reserved/capacity),
  discrete normalization at every boundary, `ResourceConverterComponent` for
  continuous and batch recipes; converter never counts workers.
- **People** — explicit `EmploymentAssignment` (workplace + role + shift), fatigue with
  exhaustion latch, duty history, `IFacilityPerformanceProvider` channel model so staffing
  publishes multipliers/blockers without knowing recipes.
- **Logistics** — `ResourceStockPolicyComponent` (foreground/background import, export
  retain), `FreightDemand`, `TransportContract`, `ContractManager`, unified
  passenger/freight arbitration in `LogisticsManager`, reservation only after a vehicle
  wins.
- **Vehicles** — `ShipComponent` (dock, phase enum, movement-owner lease), pilot lease via
  `ShipCrewDutyComponent`, `TransportExecutorComponent`, `PassengerCarrierComponent`.
- **Extraction** — `ResourceDeposit`, `ResourceCollectorComponent`,
  `ExtractionMissionController`; deliberately outside freight arbitration.
- **Lifecycle** — `SimulationManager` tick registry with priorities
  (100 staffing / 200 production / 300 logistics / 400 physical), pause/disable semantics,
  runtime registries, JSONL transition history (`ReadinessHistory`).
- **Hardening** — the readiness packet (T00–T14) fixed disabled-staffing semantics,
  containment-without-reparenting, explicit transit state, ship phases, fail-closed
  movers, teardown recovery, single duty-phase writer, availability query, registries.

## What does not exist

| Layer | Status |
|---|---|
| Player input, camera, selection | **None.** Zero hits for `Canvas`, `UIDocument`, `Input.`, `Raycast` in Runtime |
| UI | **None.** Validated command methods exist (`Assign`, pause/speed, policy mode) but nothing calls them from a player action |
| Construction / placement | **None.** The scene is a fixed diorama |
| Walkable base | **None.** Every `LocationAnchor` is an island reachable only by shuttle |
| Docking ports / queueing | **None.** One `Transform dockingPort` per ship; unlimited ships "dock" at one anchor |
| Flight model | `Vector3.MoveTowards` in a 23-line component; three callers hand-roll undock/fly/dock |
| Survival consequences | Consumption exists; hunger → penalty → death does not |
| Population growth | None |
| Game start / game over / scenario | None |
| Save/load | None (deliberately deferred; cannot slip past Phase 1) |
| Art / environment / audio | Primitives only; no materials, no environment, no audio |
| Presentation asmdef | None; nothing separates sim from visuals yet |

## Why progress feels slow

Velocity is high. The problem is **ordering**: three consecutive packets (production
refactor, staffing system, readiness remediation) were infrastructure and hardening for
a game that cannot be played. Nothing landed as *visible* progress because there is no
player in the loop to see it. The readiness packet is the tell — 15 tickets, almost all
about invariants under component-disabling experiments.

The fix is not to go faster; it is to build infrastructure only when a player-visible
epic needs it (see `ROADMAP.md` Phase 0).

## Hotspots and smells

- `People/StaffingManager.cs` — 1,063 lines, 55 methods: employment API, colonist
  reconciliation, pilot reconciliation, commute batching, schedule formatting, diagnostics.
  Every future people-feature lands here. Split is P0-A Epic G.
- Ship movement is owned by three components (`TransportExecutorComponent`,
  `ExtractionMissionController`, `ShipCrewDutyComponent`) that each call the mover and
  mutate ship phase directly. Unified in P0-S.
- `gameHoursPerRealSecond = 1` → a 24-second day. Fine for headless sim; makes any
  visible movement impossible to read. Retune to ~1/60 (P0-S §0).
- `FindObjectsByType` remains in five startup paths (acceptable) — none on hot paths.
- The Unity Test Runner has never produced a result file (licensing). Tests are treated
  as design artifacts, not gates, by explicit decision.
- The last packet could not commit (read-only `.git`); working tree was committed later
  as one squash (`c2b92cc`). Commit-per-ticket discipline is worth restoring.
