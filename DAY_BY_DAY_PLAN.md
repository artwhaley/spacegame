# Day-by-Day Plan to a Playable Minigame

26 working days plus 2 buffer days. Each day is one human-reviewable chunk, usually
1–3 agents in parallel on disjoint directories. Every day ends with **"Press Play and
see…"** — if that line isn't true, the day isn't done.

Assumptions: one human reviewer; agents do the typing; Unity opens locally to
compile/play (the Test Runner is not a gate); packets `P0-A`, `P0-S`, `P0-B` are the
locked designs referenced below.

---

## Dependency flaws this plan exposes (and where each is resolved)

| # | Glossed-over dependency | Why it breaks the naive plan | Resolved |
|---|---|---|---|
| F1 | **Everything edits `SpaceSim.unity`.** P0-A T07, P0-S S04/S05, UI, construction, environment all touch one scene file. | Parallel agents produce unmergeable scene diffs. | **Day 1**: additive scene split (`Managers`, `Base`, `Environment`, `UI`) + bootstrap loader. |
| F2 | **Presentation asmdef was scheduled inside S05 (late).** ShipView, ColonistView, port views, Facilities port, environment tumble, selection highlight all need it. | Five tickets would each try to create it. | **Day 1**: create empty `Presentation` and `UI` asmdefs once. |
| F3 | **No module prefab convention.** Docking ports (P0-S), workstation/bed slots (Facilities port), attachment nodes (construction), mesh-root separation (ShipView) all need sockets on the same prefabs. | Each later ticket would invent its own transform naming. | **Day 2**: `ModuleSockets` component + convert scene objects to prefabs. |
| F4 | **What do you place buildings ON?** There is no ground. Banished has terrain; we have a rock in space. | Construction cannot start without a placement domain. | **Day 15**: locked as node-snapping on a 2.5D plane (see decision below). |
| F5 | **No construction material exists.** Resources are Food, Ice, Water. | Construction sites would request nothing. | **Day 14**: `Regolith` resource + rock deposit. |
| F6 | **Mining ship targets exactly one deposit.** Adding Regolith means a second deposit. | Either a second mining ship + pilot (pilot-scarce) or a controller change. | **Day 14**: extraction controller picks among authored deposits by destination demand. |
| F7 | **A construction site for a corridor-connected module isn't connected yet.** Builders can't walk to it, and it has no dock port. | Every site would be shuttle-served or unreachable. | **Day 16**: site spawns a temporary EVA `TransitLink` to the nearest module within range. |
| F8 | **Staffing targets/allocator need `EmploymentRegistry`** (G split), and the HR screen needs the allocator's target column. | UI built before the allocator gets rebuilt after. | Days 3–4 split first; HR v1 (manual) Day 11; HR v2 (targets) Day 18. |
| F9 | **Flow rates need an inventory change feed.** | `InventoryComponent.OnChanged` exists — but nothing aggregates it over time. | **Day 9**: `ResourceFlowLedger`. |
| F10 | **Blocked-time reports need durations, but history records instants.** | `ReadinessHistory.Record` is fire-and-forget to JSONL. | **Day 9**: in-memory `OnRecorded` event + `BlockedTimeLedger`. |
| F11 | **The clock.** At 1 game-hour/real-second nothing is watchable, but slowing it makes a shift 8 real minutes at 1×. | Playtests must run at 4×–10×; substepping matters. | **Day 1** retune; **Day 4** substepped integrator. |
| F12 | **Death touches six systems** (population registry, employment, passenger lists, pedestrian transit, duty history, allocator). | Naive "destroy the GameObject" strands state. | **Day 20**: explicit `PopulationManager.Kill()` command that unwinds in order. |
| F13 | **Scenario bootstrap needs prefabs for everything** — which need F3 — and needs a corridor-creation path — which needs Day 17. | Bootstrap was scheduled "week 3" but couldn't have run. | **Day 22**, after all prefab/corridor paths exist. |
| F14 | **Selection needs colliders that follow moving ships.** | Ship root moves; mesh root interpolates. | Collider on the ship *root* (sim pose), highlight on mesh root. Day 7. |

### Locked on the spot (needed to plan; veto early)

- **Placement domain:** all modules sit on one horizontal plane (y = 0) around the home
  asteroid. Free XZ placement with 15° rotation snapping. A module has attachment nodes;
  a **corridor is a straight segment between two nodes**, max length authored, no
  intersections with module bounds. Modules placed beyond corridor reach are legal and
  become shuttle-served. No terrain, no grid, no vertical stacking in the slice.
- **One construction material:** `Regolith` (Discrete), mined from a rock deposit,
  delivered by freight. No fabrication chain in the slice.
- **Starting scenario:** 1 Command Pod (beds 6), 1 Farm, 1 Water Processor
  (shuttle-served, to demonstrate the trade-off), 1 Shuttle, 1 Mining Ship, ice and
  rock deposits, 8 colonists (3 Pilots, 2 Farm Technicians, 3 Builders), 48h of food.

---

## Week 1 — Make the simulation move (Days 1–6)

### Day 1 — Skeleton and scene split
- Create `ColonyPrototype.Presentation` and `ColonyPrototype.UI` asmdefs (empty),
  referencing Runtime; Runtime unchanged. Create `Assets/Art/`, `Assets/Prefabs/`.
- Split `SpaceSim.unity` into additive `Managers.unity`, `Base.unity`,
  `Environment.unity`, `UI.unity`; add a `Bootstrap.unity` that loads them. Update
  `EditorBuildSettings`.
- `SimulationManager.gameHoursPerRealSecond = 1/60`. Nothing else changes.
- Adopt commit-per-ticket.
- **Press Play and see:** the identical colony running from four scenes, at a
  watchable pace (a day takes ~24 real minutes at 1×; test at 10×).

### Day 2 — Module prefab convention
- Runtime `ModuleSockets : MonoBehaviour` (data only): `meshRoot`, `attachmentNodes[]`,
  `dockPorts[]` (transforms; components come Day 5), `workstations[]`, `beds[]`,
  `evaSpawn`. Validation in `OnValidate`.
- Convert CommandPod, Farm, Water Processor, Shuttle, Mining Ship into prefabs under
  `Assets/Prefabs/Modules|Ships/`, mesh separated from sim root, sockets authored with
  placeholder transforms. `Base.unity` contains only prefab instances.
- **Press Play and see:** same behavior; selecting a prefab instance shows sockets in
  the Scene view.

### Day 3 — Split staffing (part 1) ∥ ports ∥ flight math
- Agent A: P0-A **T00, T01, T02** (characterize, `ScheduleReportFormatter`,
  `EmploymentRegistry`).
- Agent B: P0-S **S00, S01** (`DockingPortComponent`, `DockingControlComponent`).
- Agent C: P0-S **S02** (`ShipFlightProfile`, `FlightIntegrator`, `FlightGuidance`).
- **Press Play and see:** identical behavior (no integration yet). Read the S02 report:
  a 2,000 m hop takes 60–120 game-seconds with the placeholder profile.

### Day 4 — Split staffing (part 2) ∥ voyage authority
- Agent A: P0-A **T03, T04** (`CommuteBatcher`, reconcilers). `StaffingManager` ≤ 250 lines.
- Agent B: P0-S **S03** (`ShipVoyageComponent`, substepped, phase machine).
- **Press Play and see:** in a scratch scene, a context-menu voyage pushes back, turns,
  burns, flips, brakes, aligns, captures. Staffing behaves identically.

### Day 5 — Walkable base primitives ∥ ships fly for real
- Agent A: P0-A **T05, T06** (`TransitLink`, `TransitGraph`, `RouteResolver`,
  `PedestrianTransitComponent`, `ColonistActivity.Walking`).
- Agent B: P0-S **S04** (migrate executor/extraction/crew to voyages, delete
  `ShipMovementComponent`, timed load/unload, ports + holding slots on module prefabs
  via `ModuleSockets.dockPorts`).
- **Press Play and see:** freight and passenger contracts complete via real voyages;
  the mining ship loiters at the asteroid, then berths; loading takes visible time.

### Day 6 — Corridors change behavior; acceptance
- P0-A **T07** (resolver in commutes, `Corridor CommandPod-Farm` in `Base.unity`) and
  **T08**; P0-S **S06** (runtime parts only — presentation checklist deferred to Day 12).
- **Press Play and see:** Farm staff show `Walking`, zero CommandPod↔Farm passenger
  contracts; Water staff fly. Duplicate the shuttle → one holds at the single Farm port.
  Disable the corridor → Farm commutes flip to shuttle within a shift.

---

## Week 2 — See it and touch it (Days 7–13)

### Day 7 — Camera and selection
- `Presentation/Camera/OrbitPanZoomCamera` on Input System actions (pan WASD/edge,
  orbit RMB, zoom wheel, focus-on-selection key).
- `UI/Selection/SelectionModel` (single selected kind: Facility | Colonist | Ship |
  Deposit | Port; resolves hit collider → registry entry). Colliders on sim roots
  (F14). `Presentation/SelectionHighlight` on mesh roots.
- **Press Play and see:** fly the camera; click any module, colonist, ship, or
  asteroid; it highlights and `SelectionModel.Current` names it in the Inspector.

### Day 8 — UI Toolkit shell and time controls
- `UI.unity` gets a `UIDocument`; theme (USS) with the art palette; HUD strip:
  day/hour-of-day, pause, 1×/2×/4×/10× (calling `SetPaused`/`SetSpeedMultiplier`),
  population, food/water on hand; empty right-hand selection panel; alert toast list
  fed by a `ReadinessHistory.OnRecorded` tail (F10 seam added here, Runtime).
- **Press Play and see:** control time from the HUD; toasts appear for docking,
  blocked, exhaustion.

### Day 9 — Reporting read models (Runtime `Reporting/`, tick 900)
- `ResourceFlowLedger`: subscribes `InventoryComponent.OnChanged`; per inventory ×
  resource, hourly in/out buckets over a rolling 24 game-hours; exposes
  `In24h`, `Out24h`, `NetPerHour`.
- `BlockedTimeLedger`: consumes `OnRecorded` transitions for `*.blocked`,
  `ship.phase`, `duty.*`; accumulates hours-blocked per subject × reason with open
  intervals closed on the next transition.
- `DutyReportBuilder`: from `ColonistStatusComponent` duty records → "worked 5.2h of
  8h, left Exhausted at 14:20", late arrivals, `WorkplaceUnavailable` counts.
- **Press Play and see:** Inspector on the ledgers shows farm food out/hour and
  minutes the shuttle spent Holding.

### Day 10 — Facility and ship panels (P0-B U03, U04)
- Facility panel: inventory table with 24h in/out and net/hour, converter
  recipe/progress/blocked reason, performance blockers + multipliers, staffing
  roles × shifts assigned/cap, stock-policy mode toggle (existing command), blocked
  time last 24h.
- Ship panel: voyage phase, dock/port, queue position, responsible pilot, cargo,
  current contract + `TransferProgress01`, `TryGetAvailability` reason, blocked time.
- **Press Play and see:** click the Farm → live flows; click the Shuttle → watch the
  phase change during a voyage.

### Day 11 — Colonist panel and HR screen v1 (P0-B U05, U06)
- Colonist panel: activity, duty state + blocker, fatigue bar, needs (placeholder
  until Day 20), employment, last 5 duty records in plain English.
- HR screen (full-screen modal): left = colonists (name, classes, employment, state,
  fatigue); right = workplaces × roles × shifts with assigned/cap and candidate list
  from `GetAssignmentCandidates` showing each `AssignmentResult` reason;
  Assign / Unassign buttons call the commands.
- **Press Play and see:** move a Farm Technician from shift A to B; watch them finish
  their walk, then commute at the new shift.

### Day 12 — Ships and colonists look like something (P0-S S05 part 1)
- `Presentation/Ships/ShipView` tick interpolation, `ThrusterSet` VFX from commanded
  actuation, main plume.
- `Presentation/People/ColonistView`: drives the `Person.prefab` body along
  `PedestrianTransit` (`LegOrigin` → `LegDestination` by `LegProgress01`), hides body
  when aboard a ship, shows at `evaSpawn` when arriving.
- **Press Play and see:** smooth flight at 1×–10×, RCS puffs on the correcting side;
  colonists visibly walk the corridor.

### Day 13 — Ports, holding, and the Facilities presenter
- `DockingPortView` (lights, clamps, guide-lights), `HoldingPatternView`.
- Port your `Facilities` MonoBehaviour into `Presentation/Facilities/`, adapted: reads
  `ModuleSockets.workstations/beds`, subscribes to a new
  `StaffingComponent.ActiveWorkersChanged` (the one Runtime change today), places
  Working colonists at workstation slots and Sleeping ones in beds with animation.
- **Press Play and see:** a ship queues with a blinking beacon and "Holding · #1";
  clamps close on capture; farm workers animate at stations; sleepers in bunks.

---

## Week 3 — Build (Days 14–19)

### Day 14 — Construction content and the second deposit (F5, F6)
- Content: `Regolith` (Discrete), `Builder` class, `Builder` role,
  `ConstructionRate` effect, `RockDeposit` prefab, `BuildingDefinition` SO type with
  Corridor / Habitat / Farm / Water Processor / Dock Port Module entries (prefab,
  cost, build hours, footprint bounds, max corridor length).
- `ExtractionMissionController`: choose from the **deposit registry within
  `maxRangeMeters`** (not an authored list) the deposit whose resource has the highest
  uncovered foreground demand at the unload destination (reads existing stock policy);
  otherwise the nearest with remaining stock. Newly discovered deposits then work
  without authoring (see `EXPLORATION_AND_LONG_RANGE.md` §8).
- Command Pod stock policy imports Regolith into a buffer.
- **Press Play and see:** the mining ship alternates ice and regolith runs based on
  Command Pod need; Regolith appears on the racks.

### Day 15 — Placement (F4)
- Runtime `Construction/SitePlane` (origin + normal; the starting base is plane #1)
  and `Construction/PlacementRules` evaluated against a plane (bounds overlap, node
  snapping, corridor straightness/length; corridors never cross planes),
  `ConstructionManager.TryPlace(def, pose) → PlacementResult`. Never hard-code y = 0.
- `Presentation/Construction/PlacementGhost` (valid/invalid coloring, snap preview);
  `UI` build menu listing `BuildingDefinition`s.
- **Press Play and see:** pick Habitat, drag a ghost, see it snap to nodes and turn
  red on overlap; confirm creates a **site** (empty until Day 16).

### Day 16 — Construction sites (F7)
- `ConstructionSiteComponent` composed from `LocationAnchor` + `InventoryComponent`
  (capacity = cost) + `ResourceStockPolicyComponent` (foreground import of cost) +
  `StaffingComponent` (Builder role, 1 shift by default) + `FacilityPerformanceComponent`.
  Progress += `GetMultiplier(ConstructionRate) × delta` only when all materials are
  present. Spawns a temporary EVA `TransitLink` to the nearest module node within
  `evaRange`; otherwise shuttle-served.
- Completion: instantiate the definition's prefab at the pose, transfer anchor
  identity, register sockets, destroy the site.
- **Press Play and see:** a placed Habitat requests Regolith → shuttle delivers →
  Builders walk out and work → the Habitat appears.

### Day 17 — Corridors and demolish
- Corridor `BuildingDefinition` completion creates a permanent `TransitLinkComponent`
  between the two nodes; EVA links are removed when a permanent link exists.
- `Demolish(anchor) → DemolishResult`: cancels contracts, releases reservations,
  unassigns staff (they walk/fly home), removes links, destroys.
- **Press Play and see:** build a corridor to the Water Processor — its staff stop
  flying and start walking; demolish it — they fly again.

### Day 18 — Staffing targets and the allocator (F8)
- `StaffingTarget` per role × shift on `StaffingComponent`; facility `workPriority`.
- `WorkforceAllocator` (tick 90): fills under-target roles by priority via
  `EmploymentRegistry.GetAssignmentCandidates/Assign`; releases over-target only via
  `Unassign`; skips pinned colonists; hysteresis at shift boundaries.
- HR screen v2: target ± and priority per row, "pin" toggle per colonist, an
  "allocator on/off" switch.
- **Press Play and see:** set Farm target 2 on shift B → two eligible colonists get
  hired and walk over at 08:00.

### Day 19 — Integration and economy tuning
- 72-game-hour run at 10× from `Base.unity`. Fix any stuck states (Blocked forever,
  Holding deadlock, site never completes). Tune farm curve, consumption, starting
  stock so food is sustainable for 8 colonists with 2 farm techs.
- **Press Play and see:** 72h with no permanent shortage and at least one completed
  building.

---

## Week 4 — Live, die, present (Days 20–26)

### Day 20 — Needs and death (F12)
- `ColonistNeedsComponent` (nutrition, hydration) driven by
  `PopulationResourceConsumer` satisfaction; below thresholds: slower fatigue recovery
  and a colonist-level multiplier folded into `StaffingComponent` contributions.
- `PopulationManager.Kill(colonist, reason) → KillResult`: end duty, unassign,
  remove from passenger lists/carriers, cancel pedestrian transit, record history,
  unregister, destroy. Death at nutrition 0 for N hours.
- **Press Play and see:** disable the farm → needs fall → a colonist dies → their
  workplace count drops, HR shows the vacancy, allocator refills if anyone is eligible.

### Day 21 — Housing
- `HabitationComponent` bed capacity enforced through `ModuleSockets.beds`; a colonist
  without a bed sleeps at `restfulnessMultiplier` 0.5; Habitat building adds beds;
  allocator/HR show homeless count.
- **Press Play and see:** 8 colonists, 6 beds → two rest badly → build a Habitat →
  fixed.

### Day 22 — Scenario bootstrap (F13)
- `ScenarioDefinition` SO (modules with poses, corridors as node pairs, ships, deposits,
  colonists with classes/skills/homes/assignments, starting stock, start hour).
- `ScenarioBootstrap` in `Managers.unity` spawns it into an empty `Base.unity` using
  the Day 16/17 creation paths. Hand-authored `Base.unity` content deleted.
- **Press Play and see:** New Game builds the starting colony from data; changing the
  scenario asset changes the start.

### Day 23 — Colony report and alerts (P0-B U07)
- Colony screen: population/needs summary; resource table with 24h in/out/net and
  hours-until-empty; **Blocked time** table (subject, reason, hours, last occurrence);
  **Duty failures** (exhausted departures, late arrivals, workplace-unavailable,
  unreachable commutes) as sentences; **Transport** (contracts completed/cancelled,
  avg wait, holding time per port).
- Alert rules (UI-side, from ledgers): food < 12h, colonist blocked > 2h, port holding
  > 1h, site starved of materials > 8h.
- **Press Play and see:** starve the Water Processor of a pilot and read "Water
  Processor: no eligible pilot for Shuttle shift B — 6.5h blocked" in plain English.

### Day 24 — Environment
- `ENVIRONMENT_ASSETS.md` generator: low-poly asteroid meshes/LODs/materials, 3D dust
  noise + Local Volumetric Fog prefab, dust motes, scatter into `Environment.unity`
  with clearance around the base and dock approach axes; sun + exposure pass.
- **Press Play and see:** a dirty, faceted asteroid field around the base; shuttles
  fly through dust.

### Day 25 — Session frame
- Minimal main menu (`New Game`, `Quit`), game-over screen (population zero) with the
  colony report embedded, pause on game over, hotkeys (space pause, 1–4 speed, F focus).
- **Press Play and see:** start → play → lose → read why.

### Day 26 — Playtest and balance
- Two full sessions (win-ish: 5 game-days stable; lose: over-build). Fix top 5 issues.
  Write `PLAYTEST_NOTES.md`.
- **Press Play and see:** someone else plays 15 minutes unaided and can say what the
  game is.

### Days 27–28 — Buffer
Absorb slips. If unused: audio stubs (thruster, clamps, ambient), colonist name pool.

---

## What is deliberately not in these 26 days
Save/load, power, research, morale, health, events, immigration, second site, real
art, audio, tutorial, settings, localization. Each Phase 0 ticket forbids them by name.

## Daily rhythm
Morning: dispatch 1–3 agents with README + locked design + ticket. Midday: compile,
press Play, check the day's observable. Afternoon: review diffs, commit per ticket,
update `PACKET_INDEX.md`. If the observable fails, the next day starts with the fix —
never with new scope.
