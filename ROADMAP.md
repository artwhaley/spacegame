# Roadmap to 1.0 — Banished in Space

This is an orientation map. The active implementation authority is
`planning/CURRENT_WINDOW.md`, followed by the next three days in
`DAY_BY_DAY_PLAN.md`. Current code/content at HEAD outranks this roadmap.

## Current horizon — make the colony visibly alive

The immediate horizon is the first three-day visible loop:

1. human avatars and navigable Command Post/Farm blockouts;
2. the existing interactable-facility prototype driving a visible Farm work cycle;
3. physical shuttle docking with visible boarding and disembarking.

Days 4–7 stabilize the seams that this loop exposes. Later days are questions and
experiments, not committed APIs. See `DAY_BY_DAY_PLAN.md` for the human-facing map.

## Protected spine

Keep these facts/intentions visible while the roadmap changes: inventory authority;
facility performance separate from recipes and staffing; explicit employment; pilot
lease distinct from employment; publish-then-commit freight/reservation; extraction
outside ordinary freight arbitration; logical arrived location distinct from transit;
narrow validated commands; runtime/presentation dependency direction; a behavior-
preserving staffing split; eventual one-voyage authority; and the Walk / Ship /
visibly Blocked strategic rule.

## Horizon 1 — growth and persistence

When the visible loop and the first management questions are understood, investigate
save/load, population growth, wider resource chains, power/maintenance, and exploration
as separate experiments. Their schemas and balance numbers are not current contracts.

## Horizon 2 — depth and content

Later questions include multiple sites, inter-site shipping, medical care, morale,
research, trade, events, and the presentation of cramped interiors. Add only the seam
the current slice consumes.

## Horizon 3 — productization

Performance, settings, accessibility, localization, modding, platform integration,
balance, and onboarding belong after the game loop has survived playtesting.

## Historical pre-install roadmap (reference only)

The former Phase 0–3 implementation plan remains below for reasoning and traceability.
It is not an execution queue and does not lock the old schemas, constants, or order.

---

### Historical 0. Where we actually are

| Layer | State |
|---|---|
| Content (`ScriptableObject` definitions) | Done, extensible, no-code facility composition proven |
| Economy (inventory, quantities, recipes, converters) | Done |
| People (classes, skills, employment, shifts, fatigue, duty history) | Done; `StaffingManager` is a 1,063-line hotspot |
| Logistics (stock policy, demand, contracts, unified dispatch) | Done |
| Vehicles (ships, docking phases, crew lease, executor) | Done |
| Extraction | Done |
| Lifecycle hardening (pause/disable/teardown/registries/history) | Done (readiness packet) |
| **Player input, camera, selection** | **Absent** |
| **UI of any kind** | **Absent** |
| **Construction / placement** | **Absent** |
| **Walkable base (colonists moving between adjacent facilities on foot)** | **Absent** — every anchor is currently an island reachable only by ship |
| **Survival pressure (hunger consequences, death)** | **Absent** — consumption exists, consequences don't |
| **Population growth (immigration, housing pressure)** | **Absent** |
| **Game start / game over / scenario bootstrap** | **Absent** |
| **Save/load** | Absent (deliberately deferred; cannot be deferred past Phase 1) |

The simulation is a well-built engine with no vehicle around it. Every phase below
adds player-visible capability; infrastructure only gets built when a player-visible
epic needs it.

---

### Historical 1. Architecture policy

The canonical, status-labeled policy is [`ARCHITECTURE_CONSTITUTION.md`](ARCHITECTURE_CONSTITUTION.md).
Do not copy a stale rule list into an agent prompt. Use the active three-day window and
state which rule is a verified invariant, a working policy, or a process preference.

---

### Historical 2. Phase 0 — Vertical Slice (target: 2–3 weeks)

**Definition of done:** a stranger sits down, plays 15 minutes without instruction
from you, and can say what the game is. Concretely: they place a farm, see builders
haul materials and construct it, set a staffing target, watch colonists walk to work
and back to bed, see food stock rise, then starve the colony by over-building and watch
someone die.

Epics are labeled so dependencies are explicit. Epics with no arrows between them can be
run by parallel agents.

```text
G (StaffingManager split) ──► C (Staffing targets + allocator)
A (Interaction foundation) ──► B (Construction) ──► F (Session: start/HUD/game over)
                            └─► I (Inspector & management panels)
H (Walkable base / route resolution) ──► E (Facilities presentation port)
S (Shuttle voyages: ports, 6DOF flight, presentation)   (independent; owns Vehicles/)
D (Survival needs)                     (independent)
```

### G — Pay down `StaffingManager` before anything else touches it
Extract by responsibility, keep `StaffingManager` as the facade so callers don't move:
- `EmploymentRegistry` — assign/unassign/validate/count/candidates (the mutation API).
- `ColonistReconciler` — per-colonist activity/duty-phase reconciliation.
- `PilotDutyReconciler` — ship-specific reconciliation (currently interleaved).
- `CommuteBatcher` — queue/flush grouped passenger requests.
- `ScheduleReportFormatter` — the daily schedule dump and label helpers.
Zero behavior change. This is a prerequisite because C and H both add writers here.

### H — Walkable base and route resolution
The single biggest gap for a *builder*. Colonists must move between adjacent facilities
on foot; ships are for separate bodies.
- `TransitLink` content/component: connects two `LocationAnchor`s with a mode
  (`Walk`, `Ship`) and a traversal cost.
- `RouteResolver` (runtime service): given origin/destination returns `Walk(path)`,
  `Ship(passengerContract)`, or `Unreachable(reason)`.
- `CommuteBatcher` asks `RouteResolver`; walkable commutes spawn a `PedestrianTransit`
  on the colonist (uses the explicit transit origin/destination/state from T03) instead
  of a passenger contract. Arrival commit path is shared with ship arrival.
- Movement of the body is presentation (NavMesh or spline is fine); the sim owns
  departure, ETA in game-hours, and arrival commit.
- **Locked decision: both modes are first-class and the choice between them is the
  strategic core.** Modules joined by corridors are walked; modules without a walk
  path are shuttle-served; neither → colonist is `Blocked` with a reason. Corridors
  are built once (materials, fixed geometry); shuttle links are paid for on every
  commute (pilot fatigue, vehicle availability). Runtime never balances this — layout
  and content do. Packet: `tickets/P0-A_Foundation/`.

### A — Player interaction foundation
- `UI` asmdef and `Presentation` asmdef created; Runtime untouched.
- Camera rig: orbit/pan/zoom, edge/WASD, Input System actions (asset already exists).
- Selection: raycast → `ISelectable` marker → lookup in the T09 runtime registries.
  Single selected entity exposed as a read-only `SelectionModel`.
- UI stack decision: **UI Toolkit (`UIDocument`)** for Unity 6 runtime UI unless you
  have strong uGUI muscle memory. Lock it; don't mix.
- Time controls wired to the existing pause/speed commands. Day/hour readout from
  `SimulationTime`.

### I — Inspector and management panels (read-only projections + narrow commands)
One panel per selectable kind, each a projection of existing components:
- Facility: inventory table, converter state/blocked reason, performance blockers and
  multipliers, staffing roles with assigned/active counts and **target** (from C),
  stock policy mode toggle (command exists).
- Colonist: activity, duty phase, fatigue, needs (from D), employment, last duty record.
- Ship: phase, dock, responsible pilot, cargo, current contract, availability reason
  (`TryGetAvailability` exists).
- Colony overview: population, food/water stock and 24h delta, open demand count.
Nothing in these panels writes a field. Everything they change goes through a command.

### B — Construction
Construction is a facility that imports materials and is staffed — pure reuse:
- `BuildingDefinition` (content): completed prefab, footprint, material cost
  (`ResourceAmount[]`), build hours, required Builder role, category, unlock flag.
- `ConstructionManager` with `TryPlace(def, pose) → PlacementResult`
  (overlap, terrain/attachment validity, affordability is *not* checked — materials are
  delivered, Banished-style).
- `ConstructionSiteComponent`: composed from `LocationAnchor` + `InventoryComponent`
  (capacity = cost) + `ResourceStockPolicyComponent` (foreground import of cost) +
  `StaffingComponent` (Builder role) + `FacilityPerformanceComponent`. Progress advances
  by `performance.GetMultiplier(buildRate) × delta` only when materials are complete.
  On completion: instantiate the completed prefab at the pose, hand over the
  `LocationAnchor` identity and `TransitLink`s, destroy the site.
- Ghost/placement presenter in `Presentation`; build menu in `UI`.
- Demolish command that unwinds reservations/contracts (T11 cancellation exists).

### C — Staffing targets and the workforce allocator (hybrid control)
- `StaffingTarget` per role per shift on `StaffingComponent` (content-authored default,
  player-editable through a command). Facility `workPriority` 1–10.
- `WorkforceAllocator` (`ISimulationTickable`, priority just before staffing):
  each tick, for each under-target role by descending priority, call
  `GetAssignmentCandidates` and `Assign`. Over-target or lower-priority facilities give
  up workers only through explicit `Unassign` (never mid-flight). Hysteresis so it
  does not thrash at shift boundaries.
- The allocator is a *client* of `EmploymentRegistry`; it holds no employment state.
  Turning it off returns you to fully manual mode — the two modes share one API.
- UI exposes target ± buttons and a priority slider; a colonist can still be pinned
  manually (allocator skips pinned colonists).

### D — Survival needs and consequences
- `ColonistNeedsComponent` (nutrition, hydration, `0..1`) fed from
  `PopulationResourceConsumer` results; unmet demand drains needs.
- Consequences via existing seams: needs below threshold reduce fatigue recovery and
  add a **colonist-level** work multiplier that `StaffingComponent` folds into its
  effect contribution (no new consumer coupling). Below a hard threshold the colonist
  dies: `PopulationManager` unregisters, employment is released, history records it.
- Sleep requires a bed: `HabitationComponent` capacity is enforced; homeless colonists
  rest badly (existing `restfulnessMultiplier` seam).

### E — Facilities presentation port
Your existing `Facilities` MonoBehaviour comes in as **Presentation**, adapted to the
seams here:
- Workstation/bed anchors are child transforms of a facility's `LocationAnchor`; they
  are purely visual slots (the sim has no ordered slots, so the presenter assigns
  bodies to free slots arbitrarily).
- Sim-side change is one event: `StaffingComponent.ActiveWorkersChanged`. Presenter
  subscribes, moves arrived `Working` colonists to a slot and plays the work animation
  for the remainder of the shift; `Sleeping` colonists occupy bed slots the same way.
- `ColonistView` drives the body from `ColonistAgent` transit/activity state and
  `PedestrianTransit` (from H). Views never write sim state.

### S — Shuttle voyages: ports, queueing, 6DOF flight, presentation
Shuttles are the visible heartbeat; today three components hand-roll "fly to X" over a
`MoveTowards` mover. Packet: `tickets/P0-S_Shuttle_Flight/`.
- `DockingPortComponent` + `DockingControlComponent` (per-station dock master:
  `RequestBerth → Granted | Queued | Denied`, priority-then-FIFO, holding slots).
- Sim-owned 6DOF Newtonian model (`ShipFlightProfile` content, `ShipFlightState`,
  `FlightIntegrator`, `FlightGuidance` burn-flip-burn + RCS pose-hold). No PhysX.
- `ShipVoyageComponent` — the one voyage authority: Docked → Undocking → Cruise →
  RequestingBerth → Holding → Approach → FinalDocking → Docked (or Loitering / Blocked).
  Executor, extraction and crew-return become clients.
- Timed loading/unloading; clock slowed to ~60 real-s per game-hour.
- `Presentation` asmdef: tick interpolation, thruster VFX from commanded actuation,
  port lights/clamps, holding beacons.

### F — Session: start, HUD, game over
- `ScenarioDefinition` (content): starting facilities, colonists (classes/skills),
  starting stock, day/hour. A `ScenarioBootstrap` spawns it into an empty base scene,
  replacing the hand-authored diorama.
- HUD: day/hour, speed, population, food/water with trend arrows, active alerts
  (blockers, shortages, unreachable) sourced from the T12 history stream.
- Game over when population reaches zero; summary screen from history.

### Env — Environment and art bible
Procedural low-poly asteroids (LODs, palette materials), 3D-noise Local Volumetric
Fog dust, drifting motes, scatter tool with base/dock clearance, lighting pass. Spec:
`ENVIRONMENT_ASSETS.md`. Placeholder module kit remains an open gap
(`GAPS_AND_OPEN_QUESTIONS.md`).

**Day-level sequencing of all Phase 0 epics, with dependency flaws resolved:
`DAY_BY_DAY_PLAN.md`.**

**Phase 0 explicitly does not include:** save/load, research, power, multiple sites,
morale, health, events, final art, audio, tutorial.

---

### Historical 3. Phase 1 — The Growth Loop (weeks 4–8)

**Definition of done:** a session lasts 2+ hours because the colony keeps demanding
something new. Can be saved and resumed.

- **Save/load.** `PersistentId` component on every anchor-bearing object; `ISaveable`
  on every runtime component with authoritative state (rule 11 makes this mechanical).
  Contracts, reservations, transit state and history included. Load is a bootstrap
  path parallel to `ScenarioBootstrap`. Do this first in Phase 1; every later feature
  must ship save-compatible.
- **Population growth.** Immigration ship arrives at an off-map anchor on a cadence
  tied to surplus food + free beds (passenger contracts already exist). Housing
  buildings, bed capacity pressure.
- **Economy widening.** 8–12 resources in 3 chains: Ice→Water→Food; Regolith→Building
  Material; Ore→Metal→Parts. Storage building (background-fill policy exists).
  Discrete goods for construction.
- **Power** as the second `IFacilityPerformanceProvider` channel: generators, a
  `PowerGridComponent` per connected set of anchors, brownout multipliers. Proves rule 6.
- **Maintenance and breakdowns.** Facility `ConditionComponent` decays with use, is a
  performance provider, and publishes a Maintenance role demand (class already exists).
- **Ship fleet.** Ship construction via `BuildingDefinition` (ships are buildings that
  move), second shuttle, disposition UI.
- **Skill growth** from duty history worked hours (data already recorded).
- **Exploration v1** (`EXPLORATION_AND_LONG_RANGE.md`): `ResourceField` truth vs.
  `SurveyKnowledge` belief, passive ship scanning, Sensor Mast module, volumetric scan
  view with deposit pins showing distance and round-trip time, deposit discovery,
  extraction by range. The starting ice depletes around day 5–8 so the water problem
  *is* the mid-game.
- **Alerts and history UI:** filterable event log from the JSONL history.
- **Placeholder art consistency pass** (scale convention, materials), no final art.

---

### Historical 4. Phase 2 — Depth and Content (months 3–5)

**Definition of done:** the game has its own identity; a 10-hour campaign is possible.

- **Multiple sites.** Second asteroid with its own `SitePlane` and walkable base;
  inter-site shipping uses the existing ship route mode. Route planning UI.
- **Long-range logistics and construction** (`EXPLORATION_AND_LONG_RANGE.md` §5–6):
  outpost buffers with background hauling, survey missions, two-pilot rosters, remote
  processing, resource conduits (build once, flow forever), uncrewed probes, mobile
  habitats / expeditions, prefab towing.
- **Health and medical.** Injury/illness on colonists; Clinic facility using
  Doctor/Nurse classes and the TreatmentSpeed/TreatmentOutcome effects already authored.
- **Morale** as a colonist-level provider (housing quality, variety, overwork, deaths).
- **Research** unlocking `BuildingDefinition`s and recipes; a Lab facility.
- **Trade** with an off-world market anchor: sell surplus, buy scarce inputs.
- **Events and pressure curve:** meteor damage, solar storms, equipment failure,
  refugee arrivals. Difficulty presets tune cadence and severity.
- **Scenarios and sandbox** map setup; procedural asteroid layout.
- **Final art direction, animation set through the Facilities presenter, VFX, audio.**
- **Tutorial/onboarding** built as a scripted scenario with UI highlighting.

---

### Historical 5. Phase 3 — Productization to 1.0 (months 5–8)

- **Performance:** 200+ colonists at 4× speed within tick budget; profile the
  allocator, dispatch arbitration and reconcilers; pooling for views.
- **Settings, key rebinding (Input System), accessibility, localization tables.**
- **Modding surface:** content packs are folders of ScriptableObjects; document it.
- **Platform:** Steam integration, cloud saves, achievements, build pipeline.
- **Balance pass, closed beta, bug bash, launch.**

---

### Historical 6. How to carve this into agent packets

Use the same shape as `tickets/minigame readiness/`:
`00_README_FIRST` → `01_LOCKED_DESIGN` → `T0x` tickets → acceptance matrix.

Rules for writing tickets for less-capable agents:
- **One epic per packet, one responsibility per ticket.** Epic G is four tickets
  (one extraction each), not one.
- **Lock the design before the packet starts** (the `01_LOCKED_DESIGN` doc): names of
  the new types, which asmdef they live in, the exact public API surface, and what
  they are forbidden to touch. Agents drift when the design is left open.
- **Paste the constitution (section 1) verbatim** into every ticket.
- **List the seams by file path** the ticket may modify and the files it must not.
- **State acceptance as observable behavior** in the scene at N× speed, plus the
  focused test the agent must add (tests are a design artifact here, not a gate).
- **Forbid scope creep by name**: "do not add save/load, do not add power, do not add
  morale" in every Phase 0 ticket.

Suggested first packets to run in parallel (disjoint directories):
1. **Packet P0-A: Foundation** (`People/`, `World/Transit/`) — written.
2. **Packet P0-S: Shuttle Voyages** (`Vehicles/`, `Extraction/`, `World/Docking/`,
   `Presentation/Ships/`) — written. Runtime tickets S01–S04 in weeks 1–2; S05
   presentation in week 3 alongside the Facilities port.
3. **Packet P0-B: Interaction** (`UI/`, camera, selection) — next to lock.

Then **P0-C: Build & Staff** (B + C), **P0-D: Live & Die** (D + E + F).
