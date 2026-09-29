# State of the Project (snapshot: 2026-09-25)

## Current local snapshot

The repository is on `main`. Current code and content include:

- the simulation runtime under `Assets/Scripts/ColonyPrototype` and its existing
  inventory, staffing, facility-performance, population-consumption, logistics, and
  ship components;
- the reusable `Packages/com.asteroidcolony.interactions` package, including
  `InteractableFacility`, embedded activity/sequence authoring, local motor/animation
  execution, editor validation, placement preview, and optional Animation Rigging
  contacts;
- imported Synty art under `Assets/PolygonSciFiWorlds`, converted for the HDRP project;
- the existing `Assets/SpaceSim.unity` scene and current content, which remain the
  authority for integration. The interaction package intentionally has no sample scene.

The current vertical slice now connects personal needs, physical activities, staffing,
production, inventory, local freight, and modern shuttle transport. Its key ownership
boundaries are:

- `InventoryComponent` owns shared capacity, stock, and reservation claims. Meals consume
  their own owned inventory commitment; recipes apply as one inventory-owned transaction.
- `LogisticsStockComponent` publishes explicit per-resource policies. A `FreightOrder`
  remains the parent demand while `FreightAllocation` children are sized to a real source,
  destination, and provider. A demand of 40 can be met by repeated capacity-10 shipments or
  heterogeneous 20/10/5/5 children. Intermediate staging is custody, not delivery.
- `FreightLogisticsManager` owns demand accounting and custody transitions. Walking porters,
  producer assistance, emergency employee labor, and shuttles execute through semantic
  provider contracts. A worker finishes the entire accepted final-leg quantity across trips;
  a pending shift or need release does not abandon its assigned cargo.
- `ShuttleManager` owns requests and trips; `ShuttleServiceComponent` owns physical manifest
  custody; `ShuttleVoyageComponent` alone moves the vehicle. Unload waits remain active until
  the destination inventory accepts the payload.
- The modular and commute scenes are the modern integration fixtures. Legacy extraction and
  transport remain separate and must not be activated to fill a modern logistics gap.

The slice still lacks the minimal player-facing management controls needed to start and guide
play, a construction loop, save/load, and authored observation of every physical logistics
case. Physical drone/freighter implementations are not present. Those are clear follow-up
areas rather than implied by the current provider-neutral contracts.

## Active planning state

The next step is a minimal but functional UI that lets a player start the simulation,
inspect the colony, and operate the current slice. Add further content and features one at a
time against the current runtime owners instead of building another infrastructure layer.

The external audit packet is preserved under `planning/audits/2026-09-20/` as historical
reference. Tentative notes do not override the current source or owner decisions.

## Historical pre-install snapshot (reference only)

### Historical Numbers

| Metric | Value |
|---|---|
| Runtime source | ~7,300 lines, 49 files, single `ColonyPrototype.Runtime` asmdef |
| Tests | ~3,500 lines, 23 EditMode files + 1 PlayMode |
| Commits | 22 in 4 days (Sept 14–17) |
| Content assets | 7 classes, 4 effects, 2 recipes, 3 resources, 2 roles, 1 shift pattern, 4 skills |
| Scene | `SpaceSim.unity`: CommandPod, Farm, Water Processor, Shuttle, Mining Ship 1, Ice Asteroid 1, one authored colonist (`Pilot 3`), primitives for visuals |
| Unity | 6000.5.9f1, HDRP 17.5, Input System 1.20, uGUI 2.5 (UI Toolkit modules present) |

### Historical What existed and was good (pre-vertical-slice snapshot)

The following inventory is preserved as a historical reference and does not describe the
current modern logistics or people integration:

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

### Historical What did not exist

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

### Historical Why progress felt slow

Velocity is high. The problem is **ordering**: three consecutive packets (production
refactor, staffing system, readiness remediation) were infrastructure and hardening for
a game that cannot be played. Nothing landed as *visible* progress because there is no
player in the loop to see it. The readiness packet is the tell — 15 tickets, almost all
about invariants under component-disabling experiments.

The fix is not to go faster; it is to build infrastructure only when a player-visible
epic needs it (see `ROADMAP.md` Phase 0).

### Historical Hotspots and smells

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
