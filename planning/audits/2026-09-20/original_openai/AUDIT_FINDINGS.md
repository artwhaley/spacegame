# Blank Space Audit — Adversarial Review of Fable's Tentative Spacegame Plan

**Repository:** `artwhaley/spacegame`  
**Audited planning revision:** `75e5784440f6f800c2e16241a4b59d5e500ba613`  
**Revision message:** `Add Fable's preliminary planning review and packet drafts (tentative)`  
**Audit basis:** `PROMPT_ADVERSARIAL_PLAN_AUDIT.md` supplied by the owner.

## Scope and evidence status

This is a source-and-plan audit, not a Unity runtime playtest. I inspected the planning documents and packet specs at the exact commit above and checked the real code where a planning claim needed to be distinguished from a guess.

The audit prompt says to read `PLANNING_REVIEW_FACTCHECK.md` first. That file is **not present at commit `75e5784` and is not discoverable on the repository's default branch**. I did not silently substitute imaginary fact-check results. Instead, I independently verified the load-bearing code facts used in this audit.

### Independently verified code facts

These are real constraints and are **not targets for de-specification**:

- `InventoryComponent` is the local resource quantity authority. Its mutation surface is `Add` (line 142), `Remove` (172), `Reserve` (192), and `WithdrawReserved` (225) in `Assets/Scripts/ColonyPrototype/Economy/InventoryComponent.cs`.
- `ResourceConverterComponent` explicitly consumes `FacilityPerformanceComponent` rather than counting workers itself (`Assets/Scripts/ColonyPrototype/Production/ResourceConverterComponent.cs`, comment at line 107; multiplier read at 119).
- Employment is explicit state on the colonist (`ColonistAgent.currentEmployment`, line 37) and assignment goes through `StaffingManager.Assign` (`StaffingManager.cs`, line 207).
- `ColonistAgent.currentLocation` is explicitly the last arrived location, with transit state separate (`ColonistAgent.cs`, lines 18 and 167+).
- The operating pilot is a separate temporary lease (`ShipComponent.responsiblePilot`, line 46; accessor line 56), not the employment record.
- Freight is published before commitment: `LogisticsManager` says a freight candidate becomes a contract only after it wins arbitration (line 225), and calls `CreateDemandFreightContract` only at materialization (line 409). Source reservation happens in `ContractManager` at materialization (comment line 167; `Reserve` at line 199).
- Extraction is explicitly outside ordinary logistics contracts (`ExtractionMissionController.cs`, line 14).
- `StaffingManager.cs` is in fact a ~1,064-line hotspot and contains assignment, commute, pilot-duty, colonist-reconciliation, reporting, discovery, and lifecycle concerns. A behavior-preserving responsibility split is earned work.

## Overall finding

Fable's plan is strongest where it **names existing ownership seams and proposes behavior-preserving refactors**. It becomes unsafe when the same authoritative tone is applied to systems that do not exist: flight feel, construction placement, staffing automation, survival consequences, housing, scenario schema, UI layouts, report schemas, alerts, art values, and future exploration seams.

The core failure is not that those ideas are bad. It is that the plan makes them load-bearing before the project has produced the observations that would justify them. The plan therefore converts unknowns into dependencies, and later days become pseudo-certainty built on earlier guesses.

## Classification key

- **A — Forced:** existing code or an explicit owner intent forces the decision. Keep it.
- **B — Needed now:** work is actually blocked without a temporary decision. Keep it with a revisit trigger.
- **C — Decidable later:** preserve only the seam/question/experiment and postpone the choice.
- **D — App-shaped bullshit:** fabricated constants, schemas, UI, balance, or future-proofing that exists only because the plan filled blank space.

A blank is the correct replacement whenever evidence does not yet exist.

---

# Numbered findings

## F-001 — The plan turns tentative proposals into binding authority

**Decision:** `GAME_DESIGN_DECISIONS.md` says locked decisions are binding; `ARCHITECTURE_CONSTITUTION.md` calls itself canonical; `DECISION_LOG.md` records a long list as decisions even while four of them still explicitly await owner veto.  
**Where:** `GAME_DESIGN_DECISIONS.md` line 3; `ARCHITECTURE_CONSTITUTION.md` line 3; `DECISION_LOG.md` lines 3 and 42+.  
**Class:** **D**.  
**Missing information:** owner ratification, plus observations from the unbuilt systems.  
**Last responsible moment:** immediately before an executor is allowed to implement each proposal.  
**Replacement:** rename authority states to `PROPOSED`, `RATIFIED`, and `REVISIT-TRIGGERED`. Nothing becomes binding merely because an agent wrote it into a canonical-looking file.  
**Downstream collapse:** every packet that says “locked design” must inherit only ratified A/B items; C/D items move to the decision backlog.

## F-002 — A 26-day working-depth plan is frozen before the first discovery day

**Decision:** 26 working days + 2 buffer days are specified at implementation depth.  
**Where:** `DAY_BY_DAY_PLAN.md` line 3 and Days 1–26.  
**Class:** **C**.  
**Missing information:** the output of virtually every “Press Play and see” day.  
**Last responsible moment:** each day's work should be committed one day ahead; only the next ~3 days need working depth.  
**Replacement:** rolling 3-day committed window; horizon items are questions with trigger conditions.  
**Downstream collapse:** Days 7–26 cease being implementation promises and become a trigger-gated backlog.

## F-003 — `HOW_IT_WORKS.md` narrates an unplayed game as if it already exists

**Decision:** exact day-in-the-life behavior, times, production curve, stock thresholds, automated processor, queueing behavior, death behavior, construction behavior.  
**Where:** `HOW_IT_WORKS.md`, especially 00:05 section (line 51+), 0.65 production statement (line 55), automated Water Processor (line 88), and “When you build” (line 110+).  
**Class:** **D**.  
**Missing information:** whether any of those loops are actually legible, fun, tedious, or strategically meaningful.  
**Last responsible moment:** after the relevant loop is playable and has been observed.  
**Replacement:** retain a short “what exists today” document plus a section of explicit hypotheses. Delete fictional future chronology.  
**Downstream collapse:** thresholds, staffing assumptions, flight cadence, housing expectations, and construction expectations no longer gain false legitimacy from a narrative walkthrough.

## F-004 — `GAPS_AND_OPEN_QUESTIONS.md` answers its own open questions

**Decision:** cutaway roofs, generator kit-bash, names + color + trait, and deadlines tied to the frozen schedule are proposed as answers inside the open-question file.  
**Where:** `GAPS_AND_OPEN_QUESTIONS.md`, especially line 13 and lines 62–71.  
**Class:** **D**.  
**Missing information:** actual camera, module art, interior-visibility and identity tests.  
**Last responsible moment:** when the first presentation prototype makes the cost/benefit visible.  
**Replacement:** question, trigger, experiment, current blank. No recommendation field until the experiment has run.

## F-005 — The Architecture Constitution mixes proven invariants with speculative universal laws

**Decision:** future-facing rules are given the same constitutional weight as current ownership facts: “new MonoBehaviour must be reusable by two facilities,” ~400-line ceiling, serialize all authoritative state now for future save/load, universal disabled-state semantics, etc.  
**Where:** `ARCHITECTURE_CONSTITUTION.md`, especially rules 8–12 and lines 45–51.  
**Class:** **C** (with some D constants).  
**Missing information:** actual pressure from construction, presentation, save/load, and future facility work.  
**Last responsible moment:** when those systems exist or the first violation appears.  
**Replacement:** split the Constitution into **Observed invariants** and **Provisional engineering policies**. Add revisit triggers to every provisional policy. The `~400 lines` number is a heuristic, not a law.

## F-006 — The clock retune is treated as a solved constant

**Decision:** `gameHoursPerRealSecond = 1/60`, with a target 12–24 minute day.  
**Where:** `DAY_BY_DAY_PLAN.md` Day 1; `GAME_DESIGN_DECISIONS.md` clock target (line 45); P0-0 K03; P0-S §0.  
**Class:** **D** for the number; **B** for making pace configurable/watchable.  
**Missing information:** how walking, ship flight, staffing, production and player inspection feel at different speeds.  
**Last responsible moment:** after the first watchable commute and voyage can be timed.  
**Replacement:** keep clock pace editable; run a short pace comparison; human chooses a default later.

## F-007 — `ModuleSockets` is future systems compressed into one premature schema

**Decision:** one component predeclares mesh root, attachment nodes, dock ports, approaches, holding slots, workstations, beds, EVA spawn.  
**Where:** P0-0 K04; Day 2.  
**Class:** **C**.  
**Missing information:** which consumers really need shared sockets and what their stable semantics are.  
**Last responsible moment:** immediately before the second real consumer needs the same authored transform data.  
**Replacement:** keep only the smallest currently-needed prefab convention; add socket concepts when a real consumer appears. Do not create fields solely for Days 13–21.

## F-008 — Exact starting socket counts are content guesses

**Decision:** Command Pod gets 2 ports, 2 holding slots, 8 beds, 4 nodes; Farm 1 port/2 workstations/2 nodes; Water Processor 1 port/2 nodes.  
**Where:** P0-0 K05.  
**Class:** **D**.  
**Missing information:** port contention, interior presentation, construction geometry, housing pressure.  
**Last responsible moment:** when each facility's first playable use requires a count.  
**Replacement:** placeholder content may exist for a spike, but it is not architecture and is not copied into “locked” design.

## F-009 — The walk system is over-specified beyond the earned strategic seam

**Decision:** Dijkstra by `traversalHours`, exact route-plan enums/reasons, link lookup behavior, `traversalHours = 0.25`, per-waypoint semantics, exact algorithm ordering.  
**Where:** P0-A locked design Part 2; T05–T07; Day 5–6.  
**Class:** **C** for pathfinding/route API shape; **D** for traversal constants.  
**Missing information:** whether multi-hop walking exists soon, whether travel time should be authored by links, geometry, speed, or animation, and what blockers players need to see.  
**Last responsible moment:** after one real adjacent-facility commute has been implemented and watched.  
**Replacement:** preserve only the already-earned facts: “arrival means arrived,” walking is distinct from ship transit, and current location cannot lie. First build one reversible walking spike; generalize only after observing it.

## F-010 — The starting corridor/water layout is used to prove a strategy before the strategy is played

**Decision:** CommandPod↔Farm is exactly 0.25h walking; Water Processor deliberately has no link and therefore flies.  
**Where:** P0-A T07; Day 6.  
**Class:** **D**.  
**Missing information:** what commute time is legible and whether this layout actually demonstrates a useful tradeoff.  
**Last responsible moment:** after a one-link commute can be watched.  
**Replacement:** scene content for the experiment only; no canonical starting layout until the tradeoff is reviewed by the human.

## F-011 — Finite ports may be a valid strategic idea; the queue policy is not earned

**Decision:** priority-desc/FIFO queue, holding slots, reservations with no timeout, exact startup adoption and deny reasons.  
**Where:** P0-S locked design §1; S01; Days 3–6.  
**Class:** **C**.  
**Missing information:** how often contention occurs, whether queue visibility matters, whether FIFO/priority is understandable, and whether reservations can deadlock.  
**Last responsible moment:** when two real ships can contend for one real berth.  
**Replacement:** preserve “a dock may have limited capacity” as a question/desired mechanic; implement the smallest contention spike first, then choose queue semantics.

## F-012 — “Own 6DOF Newtonian integrator, no PhysX” is a premature implementation lock

**Decision:** custom sim-owned 6DOF integrator, no `Rigidbody`, no collisions, specific guidance model.  
**Where:** `GAME_DESIGN_DECISIONS.md` flight section; `DECISION_LOG.md` #13; P0-S §2; S02.  
**Class:** **C**.  
**Missing information:** required visual fidelity, acceptable authoring burden, behavior at 10×, and whether the current movement plus presentation already meets the game need.  
**Last responsible moment:** after a movement/presentation spike compares the options in the actual scene.  
**Replacement:** a flight-feel spike; architecture remains blank until the owner watches it.

## F-013 — The flight profile is a field-by-field invented machine

**Decision:** mass 20,000 kg, thrust 60,000 N, RCS 8,000 N, torque 40,000 Nm, inertia 150,000, 40 m/s cruise, 3 m/s approach, 15 m push, 0.5 s substep, max 200 substeps.  
**Where:** P0-S locked design §2.  
**Class:** **D**.  
**Missing information:** everything those values are supposed to produce visually.  
**Last responsible moment:** tuning after a chosen flight model exists.  
**Replacement:** no canonical numbers. Spike values remain clearly disposable test fixtures.

## F-014 — The entire voyage state machine is specified before one new voyage is observed

**Decision:** exact Docked→Undocking→Cruise→RequestingBerth→Holding→Approach→FinalDocking sequence, abort rules, movement lease behavior, repolling, capture semantics, and timed transfer defaults.  
**Where:** P0-S §3–4; S03–S04.  
**Class:** **C**; transfer durations are **D**.  
**Missing information:** what states are actually required by presentation and arbitration, and where current callers truly duplicate behavior.  
**Last responsible moment:** after a single centralized-voyage refactor is proven with existing movement.  
**Replacement:** first centralize ownership without changing movement semantics; add phases only when an observable requires them.

## F-015 — Ship presentation is specified as a mini art direction before the motion model exists

**Decision:** exact light colors, clamp animation duration, beacon behavior, queue label, plume behavior, voyage trail.  
**Where:** P0-S §5; S05.  
**Class:** **D**.  
**Missing information:** final ship geometry, visual language, camera distance, and which states actually need visual affordances.  
**Last responsible moment:** after the chosen voyage model is visible.  
**Replacement:** presentation spike asks “Can I tell what the ship is doing without an inspector?”; details stay blank.

## F-016 — UI Toolkit is locked before the UI workflow is learned

**Decision:** UI Toolkit only; no uGUI/IMGUI at runtime.  
**Where:** P0-B README; `DECISION_LOG.md` #17; P0-B locked design.  
**Class:** **C**.  
**Missing information:** owner authoring comfort, iteration speed for this game, and the first management screen's layout needs.  
**Last responsible moment:** immediately before the first real management surface is committed.  
**Replacement:** one bounded UI technology spike using the same tiny panel; human chooses after touching both workflow and result.

## F-017 — Camera and input bindings are designed before the camera has been used

**Decision:** WASD/edge pan, RMB orbit, wheel zoom, F focus, later Space/1–4/Esc.  
**Where:** P0-B §1; U00; Day 7; P0-D Session; Day 25.  
**Class:** **C**.  
**Missing information:** actual camera framing, device expectations, conflict with build/selection interactions.  
**Last responsible moment:** after the first selectable scene is navigable.  
**Replacement:** minimal temporary bindings for the spike; do not canonize them.

## F-018 — The HUD is a pre-drawn product surface

**Decision:** top strip, right panel, bottom-left alerts, specific time controls, population and food/water values.  
**Where:** P0-B §3; Day 8.  
**Class:** **D**.  
**Missing information:** what the player actually looks for while watching the first loop.  
**Last responsible moment:** after a play session with debug/Inspector information reveals the recurring questions.  
**Replacement:** initially expose only the controls/data required to run the current experiment.

## F-019 — The reporting layer is schema-first instead of question-first

**Decision:** `ResourceFlowLedger`, `BlockedTimeLedger`, `DutyReportBuilder`, 24h buckets, specific summary fields, exact narrative strings.  
**Where:** P0-B §4; U02; Day 9.  
**Class:** **C**; the 24h window/string formats are **D**.  
**Missing information:** what diagnostic/player questions cannot already be answered by existing histories and components.  
**Last responsible moment:** when a real panel/report needs one aggregate that cannot be obtained safely otherwise.  
**Replacement:** add one read model per proven question, not a reporting platform in advance.

## F-020 — Facility/ship/colonist/HR screens are specified before anyone manages the colony through UI

**Decision:** tables, columns, candidate panes, filters, buttons, row trees, recent-duty format.  
**Where:** P0-B §5–6; U03–U06; Days 10–11.  
**Class:** **C** with **D** layout detail.  
**Missing information:** which management actions are frequent, painful, confusing, or unnecessary.  
**Last responsible moment:** after the first single-purpose management surface has been used in a real shift.  
**Replacement:** start with the smallest surface for one actual task; record friction; grow from use.

## F-021 — The colony report and alerts invent both questions and thresholds

**Decision:** fixed report sections/tables plus Food/Water <12h, blocked >2h, holding >1h, site starved >8h, Top(20), 4Hz evaluation.  
**Where:** P0-B §7; U07; `DAY_BY_DAY_PLAN.md` Day 23 (line 265+).  
**Class:** **D**.  
**Missing information:** which failures are genuinely hard to notice and what warning horizon is useful.  
**Last responsible moment:** after at least two sessions where the human can name missed/late information.  
**Replacement:** backlog questions only. First alert/report is added because a playtest demonstrated the need.

## F-022 — People/facility presentation is specified before interior visibility is decided

**Decision:** hide bodies aboard ships, arbitrary workstation slots, sleepers in beds, pilot walks to port, idle spots near EVA spawn.  
**Where:** P0-P locked design; Days 12–13.  
**Class:** **C**.  
**Missing information:** whether interiors are visible at all, camera scale, animation kit, actual facility geometry.  
**Last responsible moment:** after the interior-visibility experiment.  
**Replacement:** keep the presentation layer read-only; leave body-placement policy blank until there is a view to place bodies into.

## F-023 — `Regolith` and the Builder content curve are invented content masquerading as architecture

**Decision:** one construction material called Regolith; Builder class/role; min/cap/exertion; construction curve 0/0.6/1.0/1.3; target 60 buffer.  
**Where:** Day 14; P0-C Content.  
**Class:** **D**.  
**Missing information:** the first construction loop and what scarcity/cargo pressure it needs.  
**Last responsible moment:** when a construction site actually needs a material and work contribution.  
**Replacement:** use temporary test content, visibly marked disposable; human chooses content after watching one build.

## F-024 — `BuildingDefinition` is field-complete before construction has one proven use case

**Decision:** completed prefab, cost, build hours, footprint bounds, category, corridor flag, max corridor length, EVA range, unlock flag, plus a fixed entry list.  
**Where:** P0-C Content; Day 14.  
**Class:** **C**.  
**Missing information:** what placement and completion actually need; which fields are content vs. runtime state.  
**Last responsible moment:** after one building can be placed and completed by hand-authored data.  
**Replacement:** schema grows only from fields consumed by the first proven build path.

## F-025 — Placement was “locked on the spot” precisely where discovery is required

**Decision:** `SitePlane`, free XZ placement, 15° rotation snapping, node snapping, straight corridors, no terrain/grid/stacking.  
**Where:** `DAY_BY_DAY_PLAN.md` “Locked on the spot” line 32+; P0-C Placement; `GAME_DESIGN_DECISIONS.md` line 69+.  
**Class:** **C** for the domain model; **D** for 15° and exact geometry constraints.  
**Missing information:** how modules look, how they connect, what feels easy to place, whether “plane” is even visible to the player.  
**Last responsible moment:** after a placement sandbox with 2–3 placeholder module shapes is manipulated by the owner.  
**Replacement:** throwaway placement spike. Compare interaction, not architecture. Keep final placement model blank.

## F-026 — The construction site is over-shaped before placement exists

**Decision:** inventory capacity exactly equals cost; foreground priority 8; all materials before progress; Builder role/one default shift; temporary EVA link; specific blockers and completion transfer behavior.  
**Where:** P0-C Construction Site; Day 16.  
**Class:** **C** with **D** constants.  
**Missing information:** whether hauling and building overlap, whether sites need inventories, how builders reach them, what the player expects to see.  
**Last responsible moment:** after first placement and one manual material-delivery/building experiment.  
**Replacement:** preserve the good reuse hypothesis (“try to build construction from inventory + logistics + staffing”), but treat it as an experiment, not a contract.

## F-027 — Demolition behavior is written before construction behavior is known

**Decision:** cancel contracts, release reservations, unassign staff, abort voyages, remove links, refund fraction, destroy.  
**Where:** P0-C Demolish; Day 17.  
**Class:** **C**.  
**Missing information:** actual construction ownership/lifecycle and whether demolition is even needed in the first playable.  
**Last responsible moment:** after first real constructed object exists and undo/removal becomes necessary.  
**Replacement:** backlog “how is a built object safely removed?”; do not pre-author refund or unwind policy.

## F-028 — The hybrid allocator is a fully invented control model

**Decision:** target per role×shift, priority, pinning, allocator on/off, priority 90, hourly cadence, candidate stealing rules, over-target release, 4h hysteresis.  
**Where:** `GAME_DESIGN_DECISIONS.md` hybrid section; P0-C Staffing Targets; Day 18.  
**Class:** **C** for whether/how to automate staffing; **D** for algorithm and constants.  
**Missing information:** what manual staffing actually feels like with the real colony, what the owner wants automated, and which mistakes automation creates.  
**Last responsible moment:** after manually staffing several shifts/facilities in a usable UI.  
**Replacement:** keep explicit employment APIs. Observe manual management first; then human chooses whether the pain calls for targets, templates, allocator, recommendations, or something else.

## F-029 — Day 19 tunes toward a balance target nobody chose

**Decision:** sustainable food for exactly 8 colonists with 2 farm techs over 72h, plus at least one completed building.  
**Where:** Day 19.  
**Class:** **D**.  
**Missing information:** desired scarcity, session pace, farm staffing, starting scenario.  
**Last responsible moment:** after those systems are playable and the owner states the intended pressure.  
**Replacement:** tune only to expose system behavior during the current experiment; do not call it balance.

## F-030 — Needs/death is almost entirely fabricated balance

**Decision:** nutrition/hydration 0..1, same 0.4→1.0 consequence curve, alert <0.3, hydration-zero 24h death, nutrition-zero 72h death.  
**Where:** P0-D Needs; Day 20.  
**Class:** **D**.  
**Missing information:** how shortages are represented, desired lethality, recovery, player warning, individual vs. colony-level consumption.  
**Last responsible moment:** after a shortage can be created and observed without death.  
**Replacement:** first expose unmet consumption as observable state; play it; human chooses consequences later.

## F-031 — The centralized death command is a reasonable seam; its eight-step choreography is not yet earned

**Decision:** exact `PopulationManager.Kill` unwind order across duty, employment, contracts, carrier, pedestrian, pilot, history, registry/destruction.  
**Where:** P0-D Death; Day 20.  
**Class:** **C** for exact order; **B** for needing one explicit teardown entry point once death exists.  
**Missing information:** which ownership/lifecycle seams exist after walking/voyage refactors land.  
**Last responsible moment:** immediately before the first colonist can actually be removed from play.  
**Replacement:** keep “death/removal must be one validated lifecycle operation”; derive the actual unwind checklist from then-current owners.

## F-032 — Housing is designed as numbers and automation before a housing loop exists

**Decision:** beds = socket count, homeless restfulness 0.5, explicit home capacity command, Habitat adds beds, auto-home homeless hourly.  
**Where:** P0-D Housing; Day 21.  
**Class:** **D**.  
**Missing information:** whether homes are assigned, chosen, persistent, visible, or even part of the first survival slice.  
**Last responsible moment:** after sleeping/home behavior is visible and a housing shortage is intentionally created.  
**Replacement:** backlog the questions. Do not create `HabitationComponent` behavior merely to satisfy a future scenario.

## F-033 — `ScenarioDefinition` is a schema for systems that do not exist yet

**Decision:** full SO fields for planes/modules/corridors/ships/deposits/colonists/classes/skills/homes/employment/stock/start hour/clock.  
**Where:** P0-D Scenario; Day 22 (line 257+).  
**Class:** **C**.  
**Missing information:** final authored facts after construction, transit, docking, housing and staffing choices settle.  
**Last responsible moment:** after a starting colony can be assembled manually using the real runtime paths twice.  
**Replacement:** keep the hand-authored base until then; derive schema from actual authoring facts, not forecasts.

## F-034 — The exact starting colony is invented content and then used as a dependency

**Decision:** Command Pod/Farm/automated Water Processor/one Shuttle/one Mining Ship/ice+rock/8 colonists split 3-2-3/48h Food/port and bed counts.  
**Where:** `GAME_DESIGN_DECISIONS.md` Starting Scenario (line 78+); Day plan “Locked on the spot”; `HOW_IT_WORKS.md`; `DECISION_LOG.md` #27.  
**Class:** **D**.  
**Missing information:** what first-play loop is being tested and what population/facility count exposes it without noise.  
**Last responsible moment:** before the first curated playable scenario is packaged for an outside tester.  
**Replacement:** each experiment owns disposable setup content; scenario composition remains blank until the loop is understood.

## F-035 — Session frame and game-over behavior are product shape before the game loop is known

**Decision:** New Game/Quit menu, population-zero game over, embedded colony report, exact hotkeys.  
**Where:** P0-D Session; Day 25.  
**Class:** **C** with **D** key bindings.  
**Missing information:** what counts as failure, whether recovery is possible/desirable, and what information a player needs on exit.  
**Last responsible moment:** after a playable survival loop produces a real failure state.  
**Replacement:** use editor/scene launch during discovery; define session flow only when external playtest requires it.

## F-036 — The environment packet is a complete art production design before camera/composition are settled

**Decision:** generator-first workflow, exact palette hexes, asteroid scales/tri counts, crater counts, dust values, fog values, particle counts, scatter counts.  
**Where:** `ENVIRONMENT_ASSETS.md`; P0-E Day 24.  
**Class:** **C** for workflow choice; **D** for art constants.  
**Missing information:** camera distance, module art, performance, visual target in motion.  
**Last responsible moment:** after the camera and core scene composition exist.  
**Replacement:** visual reference + one tiny look-dev spike; human selects the direction before generator architecture is committed.

## F-037 — Phase-0 future-proofing for exploration forces Phase-1 design backward into current code

**Decision:** deposit registry/max range, `SitePlane` specifically for multiple sites, `operatingRole == null` reserved as automated ship semantics, environment field hook, scenario deposit timing.  
**Where:** `EXPLORATION_AND_LONG_RANGE.md` §8; P1-X stub dependencies; Days 14–16.  
**Class:** **C**.  
**Missing information:** whether the proposed exploration architecture survives the Phase-0 playable.  
**Last responsible moment:** when Phase 1 is actually being designed from the completed Phase-0 seams.  
**Replacement:** only avoid obviously irreversible coupling. Do not add semantics or abstractions solely for a hypothetical second act.

## F-038 — “Ice runs out around day 5–8” is invented campaign pacing

**Decision:** starting ice depletion is intentionally timed to create a mid-game exploration trigger.  
**Where:** `GAME_DESIGN_DECISIONS.md`; `EXPLORATION_AND_LONG_RANGE.md`; `DECISION_LOG.md` #32; ROADMAP Phase 1.  
**Class:** **D**.  
**Missing information:** actual day length, consumption, extraction throughput, desired session arc, and whether exploration is fun.  
**Last responsible moment:** Phase-1 pacing playtest.  
**Replacement:** depletion timing stays blank.

## F-039 — The multi-month roadmap is useful as themes but unsafe as committed feature shape

**Decision:** specific save/load architecture, immigration gating, economy chain counts, power grid shape, maintenance behavior, ship construction, skill growth, exploration schema, health/morale/research/trade/events, productization targets.  
**Where:** `ROADMAP.md` Phases 1–3.  
**Class:** **C**.  
**Missing information:** nearly every lesson from Phase 0.  
**Last responsible moment:** each phase boundary.  
**Replacement:** roadmap keeps outcomes/questions, not preselected architecture. P1-X's own “do not lock before Phase 0” wording is the better pattern.

## F-040 — The plan's sequencing makes guesses contagious

**Decision:** downstream work is scheduled against speculative upstream shapes rather than against verified outcomes.  
**Where:** entire `DAY_BY_DAY_PLAN.md` dependency graph.  
**Class:** **C**.  
**Missing information:** the results of the upstream experiments.  
**Last responsible moment:** every day boundary.  
**Replacement:** dependencies point to **observed outcomes** (“placement approach ratified”) rather than dates/types (“SitePlane exists”). If an upstream decision stays blank, downstream implementation stays blank too.

---

# Dependency collapse map

Removing a speculative upstream choice must remove the downstream pseudo-certainty that consumes it.

| Upstream C/D choice | Downstream work currently coupled to it | What becomes blank when upstream is blanked |
|---|---|---|
| Exact `ModuleSockets` schema | P0-S ports/approaches/holding; P0-P workstations/beds/EVA; P0-C attachment nodes; P0-D housing | Exact socket fields and content counts. Keep only sockets demanded by accepted consumers. |
| Custom 6DOF + exact voyage phases | Days 3–6 flight; Day 10 ship panel; Days 12–13 VFX/ports; Day 17 abort-on-demolish; Day 23 transport report | Flight implementation, phase-specific UI, visual effects, queue reports. Keep “one movement authority” as a refactor goal. |
| Exact walk graph/Dijkstra/traversal model | Days 5–6 commute; Day 12 walking presentation; Days 14–17 sites/corridors; Day 18 allocator commute; Day 22 scenario corridors | Path weighting, corridor authoring schema, construction integration. Keep last-arrived truth and “walk is distinct transport” seam. |
| UI Toolkit + predetermined shell | Days 8–11 all UI; Day 18 HR v2; Day 23 report; Day 25 session flow | Toolkit choice, layout, panel hierarchy. Runtime command/read-only boundaries survive. |
| Reporting schema/24h ledgers | Days 9–11 flow/blocked summaries; Day 23 report/alerts | Ledger types, fields, windows, alert rules. Add read models only when a real question requires them. |
| Regolith + `BuildingDefinition` schema | Days 14–17 construction; Day 19 tuning; Day 22 scenario modules; Day 23 site-starved alert | Material identity, content schema, site requirements, tuning targets. Construction remains a discovery epic. |
| `SitePlane` + node/15° placement | Days 15–17 sites/corridors; Day 22 scenario `sitePlanes`; P1 multiple sites | Placement domain, plane IDs, node schema, corridor geometry. Run placement spike first. |
| Hybrid allocator | Day 18; Day 20 vacancy refill; Day 22 staffing targets in scenarios; HR v2 | Targets, priorities, pinning, allocator UI/behavior. Explicit employment survives. |
| Needs/death/housing model | Days 20–21; Day 23 needs/report; Day 25 game over; Day 26 lose-session | Needs fields, thresholds, housing penalties, population-zero framing. Consumption and fatigue remain existing facts. |
| Exact starting scenario | Day 22 bootstrap; Day 23 report examples; Day 26 balance; Phase-1 depletion pacing | 3/2/3 staffing split, automated Water Processor, 48h food, bed/port counts, depletion timing. |
| Environment generator/art bible | Day 24 and P1 resource-field scatter hook | Generator architecture, palette, fog/scatter constants, future field hook. Keep only the later need for a look-dev pass. |

---

# Day-by-day classification matrix

This matrix classifies the decision groups embedded in each day. Repeated atomic details are referenced in the findings above rather than duplicated line-by-line.

| Day | Keep now (A/B) | Blank / trigger-gate (C/D) |
|---:|---|---|
| 1 | **B:** additive scene split if parallel scene ownership is actually starting; **B:** separate presentation boundary; **A/B:** preserve simulation behavior | **D:** permanent `1/60` clock; exact scene/folder ceremony not needed until consumed |
| 2 | **B:** prefab/root convention when multiple systems begin consuming prefab instances | **C/D:** full `ModuleSockets` schema and exact socket counts |
| 3 | **A:** staffing characterization and behavior-preserving extraction | **C:** docking queue API; **C/D:** flight model and tuning |
| 4 | **A:** complete staffing split with unchanged behavior | **B:** one movement/voyage authority may be earned; **C:** exact phase machine/substep behavior |
| 5 | **A:** preserve last-arrived location semantics; **B:** first walking experiment; **B:** centralize duplicated movement ownership | **C/D:** Dijkstra/traversal schema, exact docking behavior, timed transfer constants |
| 6 | **A/B:** demonstrate walk and ship as distinct modes if owner intent is retained | **D:** exact Farm corridor time, Water Processor layout, single-port contention scenario as canonical content |
| 7 | **B:** selection/camera become necessary before UI-heavy work | **C:** exact controls, selection taxonomy and highlight implementation until used |
| 8 | **B:** time control command surface; **B:** an event seam only if a real consumer needs it | **C/D:** UI Toolkit lock, HUD layout/content, alert toast taxonomy |
| 9 | **B:** add a read model only for a demonstrated question | **C/D:** three-ledger platform, 24h buckets, summary fields/string formats |
| 10 | None beyond read-only UI/validated-command architecture | **C/D:** complete Facility and Ship panel schemas |
| 11 | Explicit employment commands remain **A** | **C/D:** Colonist panel/HR layout, filters, candidate presentation |
| 12 | **B:** presentation reads sim rather than writing it | **C:** exact interpolation/VFX/body behavior until movement and camera are accepted |
| 13 | **B:** presentation needs stable read-only change notifications as consumers appear | **C/D:** port lights/clamps/holding tags, workstation/bed placement, pilot boarding choreography |
| 14 | Existing inventory/logistics/extraction primitives are **A** | **D:** Regolith, Builder numbers, ConstructionRate curve, building entry list; **C:** extraction future-proof registry behavior |
| 15 | Need some placement experiment is **B** | **C/D:** SitePlane, node snap, 15°, straight corridors, final `PlacementResult` taxonomy |
| 16 | **B:** test reusing inventory/logistics/staffing for construction | **C/D:** exact site composition, priority 8, all-material gate, EVA behavior, completion transfer |
| 17 | Safe lifecycle/removal will eventually be **B** once construction exists | **C:** demolition command choreography, refunds, corridor semantics |
| 18 | Explicit employment remains **A** | **C/D:** hybrid allocator, targets, priority, pins, tick 90, hourly cadence, 4h hysteresis, HR v2 |
| 19 | **B:** integration playtest and fix stuck states | **D:** “8 colonists + 2 farm techs sustainable” as balance target |
| 20 | **B:** observe unmet consumption; **B:** one lifecycle entry point if actual death/removal is added | **D:** needs curves/thresholds/death times; **C:** exact kill unwind order |
| 21 | None forced yet | **D:** housing capacity semantics, 0.5 restfulness, auto-home behavior |
| 22 | **B/C:** bootstrap becomes useful only after manual authoring path stabilizes | **C/D:** full `ScenarioDefinition` and all fields; exact starting scenario |
| 23 | **B:** observability should answer proven playtest questions | **D:** report schema, table columns, narrative text, all alert thresholds |
| 24 | **B:** visual look-dev after camera/core composition exist | **C/D:** generator-first workflow, exact palette/mesh/fog/particle/scatter values |
| 25 | External playtest may require minimal session framing (**B**) | **C/D:** population-zero failure, full menu/report flow, hotkeys |
| 26 | **A/B:** playtest, capture observations, choose next work from them | **C:** predeclared “win-ish/lose” session shape and balance fixes until actual loop exists |
| 27–28 | **B:** buffer/repair is fine | No speculative feature-fill if unused; unused capacity is allowed to remain unused |

---

# Ticket-level classification summary

| Packet / ticket group | Classification summary |
|---|---|
| `P0-0` K01–K06 | Scene/assembly separation **B**; clock constant **D**; minimal prefab convention **B**; full socket schema/content counts **C/D** |
| `P0-A` T00–T04 | **A**: characterize and split the 1,064-line staffing hotspot without behavior change. File-size targets are heuristics, not binding design. |
| `P0-A` T05–T08 | Last-arrived and explicit transit are **A/B**; first walking seam **B**; Dijkstra/API/reasons/traversal constants/scene layout are **C/D** |
| `P0-S` S00 | **A:** characterize current movement/owners before changing them |
| `P0-S` S01 | Limited docking capacity may be a desired mechanic; exact queue/reservation policy **C** |
| `P0-S` S02 | Custom 6DOF architecture **C**; all physical/tolerance numbers **D** |
| `P0-S` S03 | One voyage owner **B**; exact phase/cancel/substep contract **C** |
| `P0-S` S04 | Caller centralization **B**; timed transfer defaults/port content **D** |
| `P0-S` S05 | Read-only presentation **A/B**; VFX/color/timing choreography **D** |
| `P0-S` S06 | Acceptance-by-observable **A/B**; acceptance scenarios depending on C/D mechanics inherit their provisional status |
| `P0-B` U00 | Need for navigation/selection **B** when UI starts; exact controls/selection taxonomy **C** |
| `P0-B` U01 | Command/read-only separation **A/B**; UI technology/layout/HUD/alerts **C/D** |
| `P0-B` U02 | Add read model when needed **B**; prebuilt reporting platform **C/D** |
| `P0-B` U03–U06 | Entire panel/HR information architecture **C/D**; validated commands remain **A/B** |
| `P0-B` U07 | Report and alert schema/thresholds **D** |
| `P0-P` P01–P04 | Read-only presentation boundary **A/B**; body-placement/slot/boarding choreography **C** until interior/camera decisions exist |
| `P0-C` draft | Good hypothesis: reuse logistics/staffing for construction **B** as an experiment. Almost all content, schema, placement, demolition and allocator details are **C/D**. |
| `P0-D` draft | Consumption exists, but needs/death/housing/scenario/session specifics are **C/D**. Centralized removal is **B** only once removal exists. |
| `P0-E` | Environment pass is later **B**; generator implementation and art constants **C/D** |
| `P1-X` stub | **Protect the stub's explicit refusal to lock.** The detailed root exploration document is still a hypothesis bank; Phase-0 future-proofing edits are **C** unless a current seam already forces them. |

---

# Disposition

The plan should not be “fixed” by substituting a different 26-day plan. It should be **de-authoritized and shortened**:

1. Preserve the source-proven invariants and the few near-term refactors they force.
2. Commit only the next ~3 days at execution depth.
3. Turn every C/D item above into a decision-backlog entry with a trigger.
4. Promote a backlog item only after the promised experiment/observation exists and the human chooses.
5. Let downstream days disappear when their upstream assumption disappears. That is not plan failure; it is the blank doing its job.
