# Decision Log

The log preserves the reasoning trail. The status register below is the active reading
of that trail; it prevents an old proposal from silently becoming a current contract.
Append new decisions with a date and all four status fields.

## Active status register

| # | Status | Basis | Revisit trigger |
|---|---|---|---|
| 1 | OWNER DECISION | Planning direction: visible work earns infrastructure. | Revisit if the first visible loop does not improve discovery. |
| 2 | OWNER DECISION | Owner-stated game shape. | Owner changes the product identity. |
| 3 | OWNER DECISION / PROCESS POLICY | Owner accepted compile + observable behavior as the practical gate. | Revisit when the test runner is reliable and useful. |
| 4 | WORKING HYPOTHESIS | Existing interaction prototype and runtime/presentation boundary. | Day 2 integration exposes a better seam. |
| 5 | SUPERSEDED | Old 26-day milestone. | Replaced by the rolling 28-day plan. |
| 6 | OWNER DECISION | Owner-stated hybrid control concept. | Manual target experiment reveals a different need. |
| 7 | OWNER DECISION | Owner-stated walk/shuttle strategic distinction. | First real corridor commute. |
| 8 | WORKING HYPOTHESIS | Behavior-preserving refactor proposal. | Current code shows a better responsibility boundary. |
| 9 | WORKING HYPOTHESIS | Useful arrival invariant; test with the first commute handoff. | Day 7/8 walking experiment. |
| 10 | WORKING HYPOTHESIS | Scene-link proposal. | First built corridor interaction. |
| 11 | WORKING HYPOTHESIS | One voyage owner is a protected consolidation seam. | Day 3 docking and Day 9 authority experiment. |
| 12 | OPEN | Old queue policy was not earned. | Force berth contention. See DB-013–DB-015. |
| 13 | OPEN | Custom 6DOF was a proposal, not a fact. | Watch current motion; run a focused spike only if inadequate. See DB-016–DB-017. |
| 14 | WORKING HYPOTHESIS | Watchability dial, not game law. | Measure the visible slice. |
| 15 | WORKING HYPOTHESIS | Watchable transfer timing proposal. | Observe actual dock operations. |
| 16 | WORKING ARCHITECTURE POLICY | One presentation/runtime authority per fact. | Revisit if the visible loop exposes a missing owner. |
| 17 | SUPERSEDED | UI Toolkit-only was a premature global lock. | Two real management surfaces provide evidence. See DB-019. |
| 18 | WORKING HYPOTHESIS | Read-model direction. | First panel proves which facts are actually needed. |
| 19 | WORKING HYPOTHESIS | Event/read-model seam. | First diagnostic question reveals required shape. |
| 20 | SUPERSEDED | Speculative additive-scene schedule. | Split only where current consumers justify it. |
| 21 | SUPERSEDED | Future socket schema was over-specified. | Add an anchor/socket only for an immediate consumer. |
| 22 | OPEN | Placement rules were never observed. | First placement experiment. See DB-031–DB-033. |
| 23 | OPEN | `Regolith` was proposed content, not canon. | Construction needs an economy identity. See DB-029. |
| 24 | WORKING HYPOTHESIS | Registry-based extraction seam. | Exploration/content proves a different demand source. |
| 25 | WORKING HYPOTHESIS | Reuse logistics/staffing for construction. | First construction site. |
| 26 | OPEN | Fixed death teardown order was not tested. | Manual death test. See DB-004. |
| 27 | OPEN | Starting composition and bed count conflicted in old prose. | Housing/economy experiment. See DB-038–DB-041. |
| 28 | WORKING HYPOTHESIS | Avoid scenario/construction divergence. | Scenario round-trip work. See DB-037. |
| 29 | HORIZON / WORKING HYPOTHESIS | Environment-generator direction. | Repeated manual environment work becomes a bottleneck. |
| 30 | HORIZON / WORKING HYPOTHESIS | Truth/belief split for exploration. | Exploration enters the active roadmap. |
| 31 | WORKING HYPOTHESIS | Automated ship/facility handling proposal. | Current content proves the null-role meaning. |
| 32 | OPEN | Ice depletion timing was a guessed trigger. | Measured exploration/economy run. See DB-042. |
| 33 | OPEN | Future save policy was not implementation evidence. | Save/load work begins. See DB-048. |

## Historical reasoning trail (pre-install)

The original table is retained verbatim below. Its `Recorded in` links are historical
references; the status/basis/revisit fields above are authoritative.

| # | Decision | Reason | Recorded in |
|---|---|---|---|
| 1 | The project is not slow; it is mis-ordered. Build infrastructure only when a visible feature needs it. | 22 commits/4 days, ~7.3k runtime lines, zero player-facing code. | `STATE_OF_THE_PROJECT.md` |
| 2 | Genre: Banished-style survival builder. | User choice. | `GAME_DESIGN_DECISIONS.md` |
| 3 | Unity Test Runner is not a gate; tests are design artifacts inside tickets. Compile + "Press Play and see" is the gate. | User: does not care about the runner; licensing blocks batchmode. | `ARCHITECTURE_CONSTITUTION.md` rule 12 |
| 4 | The user's existing `Facilities` MonoBehaviour is ported as Presentation and adapted to `LocationAnchor`/`StaffingComponent`/`ModuleSockets` seams — the sim does not conform to it. | Keeps one authority for work state. | `tickets/P0-P_Presentation_People/` |
| 5 | Milestone: playable vertical slice ASAP (2–3 weeks → 26 working days). | User choice. | `DAY_BY_DAY_PLAN.md` |
| 6 | Control model: hybrid — targets + priority per facility, allocator fills via `Assign()`, pinning for manual control. | User choice; keeps explicit employment as the only mutation API. | `GAME_DESIGN_DECISIONS.md`, `tickets/P0-C_Build_And_Staff/` |
| 7 | **Both** corridor-walking and shuttle-service are first-class; the choice is the strategic core. Route resolver: walk if path, else ship, else Blocked — no third option; runtime never balances it. | User: "that's part of the game's strategy core." | `tickets/P0-A_Foundation/01_LOCKED_DESIGN.md` §Part 2 |
| 8 | Split `StaffingManager` into 5 classes behind an unchanged facade, before adding writers. | 1,063 lines / 55 methods; allocator and walking both land there. | `tickets/P0-A_Foundation/` T01–T04 |
| 9 | Per-waypoint arrival commits for pedestrians; closed corridor = `Blocked`, never a fallback. | "Arrival means arrived"; trivial re-routing. | P0-A locked design |
| 10 | Corridors are scene components (`TransitLinkComponent`), not ScriptableObjects; their cost lives in the corridor `BuildingDefinition`. | Construction owns cost. | P0-A locked design |
| 11 | One voyage authority (`ShipVoyageComponent`): undock → cruise → request berth → hold → approach → capture. Executor/extraction/crew become clients. | Three components hand-rolled the same sequence. | `tickets/P0-S_Shuttle_Flight/01_LOCKED_DESIGN.md` |
| 12 | Docking ports are finite per station with a priority-then-FIFO queue and holding slots; port count is a build decision. | User requirement; strategic depth. | P0-S locked design §1 |
| 13 | **Sim-owned 6DOF Newtonian integrator, no PhysX `Rigidbody`.** No collisions. | Deterministic, pause/speed-safe, testable, serializable. Flagged for veto. | P0-S locked design §2 |
| 14 | **Slow the clock** to ~1 game-hour per 60 real seconds. | At 1 h/s nothing is watchable; all rates are per game-hour so nothing else changes. Flagged for veto. | P0-S §0, `tickets/P0-0_Skeleton/` K03 |
| 15 | Loading/unloading take game-time; inventories still mutate atomically at phase end. | Watchable without breaking conservation. | P0-S §4 |
| 16 | Presentation and UI are separate asmdefs; Runtime never references them; deleting them leaves the sim identical. | Rule 1/2. | `ARCHITECTURE_CONSTITUTION.md` |
| 17 | UI stack: UI Toolkit (`UIDocument`), not uGUI. | Unity 6 runtime UI; lock to avoid mixing. | `tickets/P0-B_Interaction_UI/` |
| 18 | UI numbers come only from read models (`ResourceFlowLedger`, `BlockedTimeLedger`, `DutyReportBuilder`) — panels never aggregate. | One owner for every displayed number; human-readable reports. | P0-B locked design §4 |
| 19 | Only Runtime additions for UI: `ReadinessHistory.OnRecorded` event and the `Reporting/` folder. | Keep UI a projection. | P0-B |
| 20 | Additive scene split (`Managers`, `Base`, `Environment`, `UI`, `Bootstrap`) with per-packet ownership. | Parallel agents were all editing `SpaceSim.unity`. | P0-0 K02, `PACKET_INDEX.md` |
| 21 | `ModuleSockets` convention on every prefab (mesh root, nodes, ports, approaches, holding slots, workstations, beds, evaSpawn). | Ports, presenter, construction, ship view all need the same sockets. | P0-0 K04 |
| 22 | Placement domain: node-snapping on a `SitePlane` (origin+normal), 15° rotation snap, corridors are straight node-to-node segments; no terrain, no grid, no stacking. Never hard-code y = 0. | There is no ground in space; terrain is a month of work; planes generalize to outposts. | `DAY_BY_DAY_PLAN.md` "Locked on the spot", P0-C draft |
| 23 | One construction material in the slice: `Regolith`, mined from a rock deposit. No fabrication chain. | Construction needs a material; keep the slice small. | Day 14 |
| 24 | Extraction picks deposits from the registry within `maxRangeMeters` by destination demand, not from an authored list. | Discovered deposits work instantly later. | Day 14, `EXPLORATION_AND_LONG_RANGE.md` §8 |
| 25 | Construction site = anchor + inventory (capacity = cost) + foreground import policy + Builder staffing + performance-driven progress; temporary EVA link to nearest module in range. | Reuses freight and staffing wholesale; no wallet. | P0-C draft |
| 26 | Death is one explicit `PopulationManager.Kill()` that unwinds six systems in a fixed order. | Destroying the GameObject strands state. | P0-D draft |
| 27 | Starting scenario: Command Pod (8 beds, 2 ports), Farm (corridor), Water Processor (shuttle-served, **automated**), Shuttle, Mining Ship, ice + rock deposits, 8 colonists (3 Pilots, 2 Farm Techs, 3 Builders), 48h food. | Demonstrates the walk/ship trade-off through freight without spending a colonist. | `GAME_DESIGN_DECISIONS.md`, `HOW_IT_WORKS.md` |
| 28 | Scenario bootstrap builds the start through the construction completion path, never by hand-placing components. | Scenario and construction cannot diverge. | P0-D draft |
| 29 | Environment is a seeded Editor generator (asteroids, dust `Texture3D` → HDRP Local Volumetric Fog, motes, scatter with dock clearance), Synty-adjacent faceted look. | Iterate the look in minutes; consistent conventions. | `ENVIRONMENT_ASSETS.md` |
| 30 | Truth vs. belief split for exploration: `ResourceField` (truth) and `SurveyKnowledge` (belief); UI reads belief only. | Rule 4 applied to fog-of-war. | `EXPLORATION_AND_LONG_RANGE.md` |
| 31 | `ShipComponent.operatingRole == null` is reserved to mean *automated/uncrewed*; never treat it as an error. | Future probes mirror automated facilities. | `EXPLORATION_AND_LONG_RANGE.md` §8 |
| 32 | Starting ice is sized to deplete around day 5–8; the water problem is the designed mid-game. | Gives the exploration/long-range act a trigger. | ROADMAP Phase 1 |
| 33 | Save/load deferred to Phase 1 week 1, but rule 11 (serialized authoritative state) is enforced from now. | Make save a serializer, not a rewrite. | Constitution rule 11 |

## Vetoes requested from the owner (not yet answered)
- #13 sim-owned integrator vs. PhysX.
- #14 clock retune to ~60 s per game-hour.
- #22 placement domain.
- #27 starting scenario composition (incl. automated Water Processor).
