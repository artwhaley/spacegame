# Game Design Decisions

Planning statements use four statuses:

- **FACT** — verified in current code or content;
- **OWNER DECISION** — deliberately chosen product intent;
- **WORKING HYPOTHESIS** — a provisional choice made to enable the next observable;
- **OPEN QUESTION** — no answer has been earned yet; see `DECISION_BACKLOG.md`.

## Verified / Owner Decisions

### FACT — current seams

- `InventoryComponent` remains the resource-quantity authority; views and reports do
  not carry a second stock truth.
- `EmploymentAssignment` and the validated staffing commands remain the employment
  authority.
- `ColonistAgent.currentLocation` is the last arrived logical location; transit and
  presentation movement are separate.
- `FacilityPerformanceComponent` aggregates provider channels; converters do not count
  workers themselves.
- `PopulationResourceConsumer` currently consumes aggregate resources from a
  habitation inventory. It has no per-colonist `Fed(...)` API or personal-needs model.
- The Farm Operator content asset already ships a one-worker `0.65` performance curve
  (`Assets/GameData/Roles/FarmOperator.asset`); it is not an invented planning constant.
- `HabitationComponent` has an explicit `capacity` and `restfulnessMultiplier` seam.
  No homelessness penalty is ratified by that seam.
- The reusable interaction authoring unit is the local
  `Packages/com.asteroidcolony.interactions` package. Its `InteractableFacility`
  owns activity and sequence data; the package contains no game-specific sample scene.

### OWNER DECISION — product intent

- The game is a small colony-builder in which the player should physically see people
  moving, working, sitting, sleeping, boarding, and disembarking.
- Walk and shuttle transport are both first-class strategic modes. A valid walk
  connection should matter to the layout; lack of either mode should be visible rather
  than silently bypassed.
- Staffing is a facility-level concern separate from recipe definitions and facility
  production authority.
- Hybrid staffing control is a valid direction: human-set targets/priority and manual
  pinning may coexist. The autonomous allocation algorithm is not chosen.
- Explicit employment remains the mutation authority, and a pilot lease remains
  distinct from employment.

## Working Hypotheses

- Days 1–3 should integrate human avatars, navigable facility blockouts, the existing
  interactable-facility prototype, and visible shuttle docking before expanding the
  simulation. The active details live in `planning/CURRENT_WINDOW.md`.
- A local work-cycle layer should present what an already-active worker does at a
  facility. It must not become a second authority for employment, recipes,
  productivity, inventory, or resource quantities.
- One voyage authority is a useful refactor seam, but the final flight model and
  docking phases must be learned from the visible docking experiment.
- The fastest UI technology that answers the first real management question should be
  used; UI Toolkit is not a project-wide lock.
- Scenario/bootstrap data should be derived from the slice that actually exists when
  that work begins, rather than predicted as a full schema now.

## Open Questions

The active unresolved questions and their evidence triggers live in
[`DECISION_BACKLOG.md`](DECISION_BACKLOG.md). In particular, do not lock:

- personal nutrition/hydration, ration allocation, death thresholds, or a homelessness
  penalty;
- custom Newtonian 6DOF, a final docking queue policy, or modulo holding-slot behavior;
- UI Toolkit as a global standard, a canonical construction material, or a full
  `ScenarioDefinition` field list;
- starting crew/stock/bed counts, including the old 6-vs-8 bed discrepancy;
- the final interior/cutaway presentation technique or construction-site access rule.

## Historical pre-install register (reference only)

The older locked/open register below is retained as reasoning history. It is not an
execution contract; the active registers above and the decision backlog supersede it.

### Identity
- **Genre:** Banished-style survival builder on an asteroid colony. Player places
  facilities, sets staffing targets, keeps colonists fed/rested/housed while population
  grows. Failure is attrition; game over at population zero.
- **Ships and logistics are connective tissue, not the game.** They must look great
  because they are what the player *watches*, but the decisions are about layout and
  people.

### The strategic core: corridors vs. shuttles
- Modules joined by an **open corridor path are walked** — free forever after
  construction, but corridors cost materials and fix relative geometry.
- Modules with **no walk path are shuttle-served** — cheap to place anywhere, but every
  commute and every delivery costs pilot fatigue and shuttle availability, and ships
  queue for docking ports.
- **Neither** → the colonist/cargo is visibly `Blocked` with a reason. The runtime never
  invents a third option and never balances the trade-off; layout and content do.
- Docking ports are finite per station; ships **hold** in a queue when none is free.
  Port count is a build decision.

### Control model: hybrid
- Player sets a **target headcount per role per shift** and a **priority** per facility.
  A `WorkforceAllocator` fills targets through the existing explicit `Assign()` API.
- Player may **pin** an individual colonist; the allocator skips pinned colonists.
- Turning the allocator off yields fully manual mode. Both modes share one API.
- No RimWorld-style self-directed job picking.

### Construction
- Placing a building creates a **construction site** that *requests materials* via the
  freight system and *needs Builders* via staffing. No global wallet; the colony
  physically builds things.
- Completion swaps the site for the real facility and hands over its anchor and links.
- Demolish is an explicit command that unwinds reservations and contracts.

### Time and shifts
- 24-hour day, three explicit 8-hour shifts (A/B/C). An unassigned shift is a dark
  facility, never auto-filled.
- Absolute game-hours everywhere in simulation; hour-of-day is a view.
- Clock target: **~1 game-hour per 60 real seconds at 1×** (12–24 real-minute day).
  Speeds 1×–10×.

### People
- Classes are hard eligibility; skills are bonuses; a colonist has exactly one
  employment.
- Fatigue: +0.10/h working (× role exertion), −0.10/h sleeping (× home restfulness);
  exhaustion latch at 0.90, clears at 0.20; exhausted workers leave immediately.
- Needs (Phase 0 Epic D): nutrition and hydration 0..1 fed by consumption; unmet needs
  reduce recovery and add a colonist-level work penalty; hard threshold → death →
  employment released, history recorded.
- Sleep requires a bed; homeless colonists rest badly.
- No auto-vacancy filling, no call-ins, no teleporting; reassignment finishes the
  current flight first.

### Flight and docking
- Voyage sequence: load → undock → cruise → request berth → hold if queued → approach →
  capture → unload. One authority (`ShipVoyageComponent`).
- **Sim-owned 6DOF Newtonian model** (own integrator, no PhysX). Deterministic,
  pause-safe, speed-safe. No collisions, fuel, damage, gravity, or player autopilot.
- Loading/unloading take game-time and are watchable.

### Placement and the world
- All modules on a **`SitePlane`** (origin + normal); the starting base is plane 1.
  Free XZ placement with 15° rotation snapping; modules have attachment nodes; a
  **corridor is a straight segment between two nodes** with an authored max length and
  no footprint intersections. No terrain, grid, or vertical stacking in the slice.
  Corridors never cross planes; ships do.
- One construction material in the slice: `Regolith` (Discrete), mined from a rock
  deposit and delivered by freight.
- Deposits are finite. The starting ice is sized to run out around day 5–8 so "where
  do we get water now" is the designed mid-game (`EXPLORATION_AND_LONG_RANGE.md`).

### Starting scenario (the minigame)
Command Pod (8 beds, 2 docking ports, main warehouse), Farm (corridor to the Pod),
Water Processor (shuttle-served, **automated** — no staffing role), 1 Shuttle, 1 Mining
Ship, ice and rock deposits, 8 colonists (3 Pilots, 2 Farm Technicians, 3 Builders),
48 hours of Food. Walkthrough: `HOW_IT_WORKS.md`.

### Presentation principles
- Views are read-only projections. Deleting every presentation component leaves the
  simulation identical.
- "Arrival means arrived": bodies and ships visibly reach places before the sim commits.
- Art direction: low-poly, faceted, **Synty-adjacent** (hard-shaded, restrained palette,
  readable silhouettes) — not full Synty. Dirty, dusty space: volumetric dust,
  drifting motes, muted warm greys with accent lighting.

### Historical open register (superseded; see active backlog)

| Question | Why it matters | Owning phase |
|---|---|---|
| Interior visibility: do we see colonists inside modules/corridors (cutaway, x-ray, windows) or only exteriors? | Drives the entire module art kit and the Facilities presenter | Before P0-D |
| Module kit: fixed footprint pieces (hab, farm, processor, corridor segment, port module) vs. freeform? | Construction placement rules, corridor authoring, art scope | P0-C design |
| Resource chains beyond Ice→Water→Food; which construction material(s) exist first? | P0-C needs at least one buildable material | P0-C design |
| Immigration cadence and what gates it (food surplus, free beds, both) | Phase 1 growth loop | Phase 1 |
| Corridor pressurization / damage as a mechanic? | Reuses "closed link = blocker" seam; scope risk | Phase 1–2 |
| Colonist identity depth (names, portraits, traits) | Makes death matter; art cost | Before P0-D |
| Difficulty presets and initial scenario numbers | Balance | After first playable |
| Multiple sites / inter-asteroid shipping | Phase 2 | Phase 2 |
