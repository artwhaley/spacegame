# Luna's Adversarial Blank-Space Audit

**Reviewer:** Luna  
**Date:** 2026-09-20  
**Subject:** Fable's tentative 26-day plan, packets, and authority documents  
**Status:** VETO-ABLE review; not an approval and not a replacement decision set.

## Executive verdict

The plan is mechanically careful and strategically promising. Its daily cadence, visible observables, dependency analysis, and protection of existing runtime authorities are strong. The failure is register, not competence: facts, owner decisions, seams, placeholders, and guesses are written in the same voice. A reader or implementation agent cannot reliably tell what must survive from what merely sounded plausible during planning.

The plan therefore spends its certainty too far ahead of evidence. Days 1–6 are mostly justified infrastructure and characterization. Days 14–26 become increasingly coupled to unplayed assumptions: the placement interaction, construction content, allocator behavior, personal needs, death, scenario schema, report layout, alert thresholds, and starting balance. The remedy is not to replace Fable's guesses with Luna's guesses. Keep the seam, blank the answer, attach an observable trigger, and let the owner decide.

### Highest-priority findings

1. **P0-D personal needs is not a seam yet.** `Fed(colonist, resource, fraction)` is not present in the codebase; current population consumption is habitation-level and aggregate. The personal-needs fork must be decided experimentally before a colonist needs panel, work penalty, or death timer is designed.
2. **The scenario schema is a dependency nexus.** The fifteen-field `ScenarioDefinition` mirrors several unbuilt systems and would freeze their guesses into a type. Prefer a capture/replay spike or the smallest schema proven by the first built scenario.
3. **The plan's locked framing is itself load-bearing.** `01_LOCKED_DESIGN.md`, “Locked on the spot,” `GAME_DESIGN_DECISIONS.md`'s `## Locked`, and the Constitution's “binding” language conflict with the TENTATIVE banners. Rename or demote them before dispatch.
4. **The 26-day schedule is not a rolling learning loop yet.** It has observables, but most days do not state what the observable is meant to teach, and later days are committed before earlier learning exists.
5. **The audit itself must protect facts.** The Farm's `0.65` staffing curve and fatigue values are existing serialized content, not invented constants. The existing `HabitationComponent.restfulnessMultiplier` is a real seam; only a proposed homeless value is a guess.

All findings below are VETO-ABLE. “Replacement” means the blank or minimal seam that should exist instead; it is not a new design to accept automatically.

---

## Part A — Findings

### A. Forced or needed structure to keep

**F-01 — Preserve the staffing facade split.**  
**Where:** `ROADMAP.md` Epic G; `tickets/P0-A_Foundation/01_LOCKED_DESIGN.md`, T00–T04; Days 3–4.  
**Class:** A.  
The 1,063-line hotspot and the existing public API make characterization followed by extraction behind `StaffingManager` a forced, low-risk seam. Keep the zero-behavior-change requirement. The line target is not a product requirement; validate responsibilities and behavior first.

**F-02 — Preserve the authored-link route seam, not its tuning values.**  
**Where:** P0-A Part 2; Day 5–6.  
**Class:** A/B.  
The code needs an explicit answer distinguishing walking, passenger transport, and unreachable state, and the owner explicitly wants corridor-vs-shuttle to matter. Keep `TransitLink`/`RouteResolver` and “arrival means arrived.” Do not treat `traversalHours = 0.25`, EVA range, path costs, or the exact fallback wording as settled balance.

**F-03 — Preserve one voyage authority and publish-then-commit.**  
**Where:** P0-S design; `ARCHITECTURE.md`; Days 3–6.  
**Class:** A/B.  
Three existing movement callers and the existing logistics authority justify one voyage state machine and deferred reservations. The exact 6DOF magnitudes, docking tolerances, load times, holding geometry, and presentation effects remain experiment inputs.

**F-04 — Preserve additive scenes and daily Play observables.**  
**Where:** P0-0 K02; `DAY_BY_DAY_PLAN.md` daily rhythm.  
**Class:** A/B.  
The scene collision problem is real, and “Press Play and see” is the owner's preferred discovery mechanism. Add a learning question and a predecessor gate; do not turn the scene split into a reason to pre-author every socket or scenario field.

**F-05 — Preserve read-model intent, not the report schema.**  
**Where:** P0-B locked design §4; Day 9; Day 23.  
**Class:** A/C.  
UI should not calculate authoritative aggregates. Ledgers/read models are a sound seam. `24h`, bucket width, `Top(20)`, sparkline shape, sentence wording, and alert thresholds are not forced by that seam and must be measured or left blank.

### B. Invented or premature decisions

**F-06 — Needs are personal before the model proves they should be.**  
**Where:** `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` Needs; Day 20; Day 11 colonist panel; `HOW_IT_WORKS.md` §2.  
**Class:** D for the asserted API, C for the product question.  
`Fed(colonist, resource, fraction)` does not exist. The current `PopulationResourceConsumer` consumes from habitation entries and records aggregate satisfaction/shortage; it cannot name a colonist. Missing information: whether the game needs to tell the player *who* is hungry, rather than that the colony is short. **Last responsible moment:** before Day 11 reserves UI space and before Day 20 chooses the data model. **Replacement:** leave personal needs blank; expose existing aggregate shortage first; run a shortage playtest and record whether individual attribution is wanted.

**F-07 — Death timers are invented balance.**  
**Where:** P0-D Needs; Day 20; `GAME_DESIGN_DECISIONS.md` People; `HOW_IT_WORKS.md` §2.  
**Class:** D.  
Nutrition at zero for 72h and hydration at zero for 24h are not derived from existing code or observed play. Missing information: warning time, desired emotional shape, and whether failure should be one dramatic death or gradual attrition. **Last responsible moment:** after a first playable shortage run. **Replacement:** implement only a visible shortage/critical transition or a debug-only death path for measurement; choose thresholds after watching, then remove the debug switch before shipping.

**F-08 — Homeless restfulness `0.5` is a guess; the seam is real.**  
**Where:** P0-D Housing; Day 21; `HabitationComponent.restfulnessMultiplier`; `StaffingManager.HomeRestfulness`.  
**Class:** D value / A seam.  
The existing runtime seam must remain. Do not silently convert it into a `0.5` design value before observing housing pressure. **Last responsible moment:** when a real bed shortage is visible. **Replacement:** keep `capacity` authoritative on `HabitationComponent`; label the homeless multiplier pending and make the first observable “identify unhoused colonists,” not “prove 0.5 feels right.”

**F-09 — The impaired-worker aggregation rule is missing.**  
**Where:** P0-D work multiplier; `StaffingComponent`; Day 20.  
**Class:** C/D.  
The plan says a colonist-level multiplier is folded into staffing while counts remain integer, but existing staffing curves evaluate active worker counts and skill bonuses; the proposed fractional contribution semantics are not an existing seam. Missing information: average effectiveness, fractional headcount, or another rule. **Last responsible moment:** after a needs/impairment experiment. **Replacement:** backlog the aggregation question; do not add `WorkEffectiveness` or change curve signatures until the first impaired worker has been watched.

**F-10 — `Kill()` needs an invariant, not a prescribed eight-step choreography.**  
**Where:** P0-D Death; Day 20.  
**Class:** B command / C order.  
A single validated death command is needed because death spans employment, contracts, carriers, pilot leases, transit, history, and registries. The fixed order is premature and the current `OnDestroy` already handles some unregistering. **Last responsible moment:** when the first real death path exists. **Replacement:** acceptance is “after `Kill`, no contract/carrier/lease/transit/ledger holder still references the colonist, a cause is recorded, and no duplicate cleanup occurs.” Discover holders by inspection on the day.

**F-11 — Placement is a mouse experiment, not a locked geometry system.**  
**Where:** Day 15; P0-C Placement; `GAME_DESIGN_DECISIONS.md` Placement.  
**Class:** D for the rule list, C for the mechanism.  
15° rotation, node snapping, free-placement fallback, plane IDs, straight corridor rules, maximum lengths, eight result values, and “locked” placement states are specified before a ghost has been dragged. **Last responsible moment:** when the first placement ghost is playable. **Replacement:** a pure `Evaluate(definition, pose, current geometry)` seam with the smallest result set needed by the first observable; decide snap/free behavior by dragging, and add corridor rules when a corridor is actually placed.

**F-12 — `SitePlane` is over-generalized.**  
**Where:** Day 15; P0-C; `EXPLORATION_AND_LONG_RANGE.md` rationale.  
**Class:** C/D.  
A plane value may be useful for the first scene, but `planeId`, cross-plane rules, and outpost generalization are Phase 2 speculation. **Last responsible moment:** when a second physical site exists. **Replacement:** one explicit placement domain with origin/normal if required; no multi-plane identity until a second site makes it necessary.

**F-13 — Regolith and the building catalog are not the same decision.**  
**Where:** Day 14; P0-C Content; `GAME_DESIGN_DECISIONS.md` Open resource question.  
**Class:** C/D.  
A build material is needed only when the first site needs a cost. The plan also preselects Farm, Water Processor, Dock Port Module, categories, unlock flags, and a mining heuristic. **Last responsible moment:** the first construction job. **Replacement:** author the minimum resource and minimum building entries required by the first observable; leave the resource chain, catalog, category, and unlock model blank. Note that Day 14 currently answers an open question that the open-question document says not to resolve in a ticket.

**F-14 — Deposit auto-selection is an unearned autonomy choice.**  
**Where:** Day 14; `EXPLORATION_AND_LONG_RANGE.md` §8.  
**Class:** D algorithm / B need.  
The second deposit may be needed, but a registry + range limit + “highest uncovered foreground demand” ranking is not. Existing extraction has a target deposit. **Last responsible moment:** when two deposits are visible and the player must choose. **Replacement:** keep one explicit target for the slice or create a player command; backlog automatic ranking as a separate experiment.

**F-15 — The allocator is specified before manual staffing has been felt.**  
**Where:** P0-C allocator; Day 18; `GAME_DESIGN_DECISIONS.md` hybrid control.  
**Class:** A for the owner's target/priority/pin choice; D for algorithm details.  
Priority 90, hourly throttling, release order, candidate exclusion, four-hour hysteresis, and “allocator changed” UI are guesses about a player-facing behavior. **Last responsible moment:** after the manual HR screen has been used through a shift change. **Replacement:** first ship targets, priority, pin state, commands, and a visible gap. Backlog autonomous movement/release/thrash rules until observed.

**F-16 — `ScenarioDefinition` freezes six unbuilt systems.**  
**Where:** P0-D Scenario; Day 22.  
**Class:** B for bootstrap-through-common-path; D for the field list.  
The proposed schema embeds planes, module records, staffing targets, corridor node indices, port indices, deposits, employment, and clock defaults before those concepts have stabilized. **Last responsible moment:** after construction and one hand-built playable base exist. **Replacement:** run a round-trip spike: capture the live base into the smallest asset needed to replay it through the same creation path. If hand-authored fields are still needed, add them only when the capture/replay observable proves the need.

**F-17 — Bed capacity must not be owned by presentation sockets.**  
**Where:** P0-0 K04/K05; P0-D Housing.  
**Class:** C.  
`HabitationComponent.capacity` is the runtime authority today. Making `ModuleSockets.beds.Count` the economy silently lets art edits change housing. **Last responsible moment:** before prefab conversion. **Replacement:** runtime capacity remains authoritative; sockets provide visual slots up to capacity; mismatch is an authoring error.

**F-18 — HR and colony-report layouts are app-shaped UI.**  
**Where:** P0-B §5–7; Days 10–11 and 23; P0-D Day 25.  
**Class:** D.  
The two-pane HR tree, candidate reason list, full report tables, sparkline, Top(20), modal structure, and game-over embedded dashboard are all prescribed before the colony has been played. **Last responsible moment:** the first management task and first loss. **Replacement:** expose the smallest read-only projection and one command; let the first player task decide whether the screen is person-first, job-first, or number-first. Keep command result reasons as a strong candidate because they serve the existing narrow-command architecture.

**F-19 — Alert values and hotkeys are not design knowledge.**  
**Where:** Day 23; P0-B §3/§7; P0-D Session; Day 25.  
**Class:** D.  
`12h`, `2h`, `1h`, `8h`, `Space`, `1–4`, `F`, and `Esc` are implementation defaults, not earned decisions. **Last responsible moment:** after a normal 72h observation for alerts, and on first input pass for keys. **Replacement:** alert infrastructure accepts empty/pending definitions and logs distributions; choose thresholds from observed p50/p95/max. Pick keys when the shell is touched.

**F-20 — Walkthrough prose deletes the blank.**  
**Where:** `HOW_IT_WORKS.md` §2 and §5; `GAPS_AND_OPEN_QUESTIONS.md` recommendations; `EXPLORATION_AND_LONG_RANGE.md` §1–6.  
**Class:** D as framing.  
The minute-by-minute narrative states unplayed topology, staffing, water thresholds, export thresholds, production rates, deaths, and future behavior in the present tense. The gaps document sometimes supplies recommendations where the owner asked for open questions. **Last responsible moment:** now, before packet dispatch. **Replacement:** rewrite as “hypothesis / observable / unknown”; move numbers to the backlog; retain only forward-looking seams in the exploration document and label its content as horizon, not slice specification.

**F-21 — Authority documents contradict their own tentative status.**  
**Where:** banners in `ARCHITECTURE_CONSTITUTION.md`, `GAME_DESIGN_DECISIONS.md`, `HOW_IT_WORKS.md`, and packet `01_LOCKED_DESIGN.md`; `ROADMAP.md` duplicated constitution.  
**Class:** D framing plus documentation defect.  
“Nothing here is locked” sits above `## Locked`; “proposed” sits above “binding”; `ROADMAP.md` repeats rules and contains the non-existent `ShipPhase` name while the canonical constitution names `ShipMovementPhase`. **Last responsible moment:** before dispatch. **Replacement:** one canonical constitution linked from all documents; rename locked artifacts to working/proposed; demote only unearned rules, not proven runtime invariants.

**F-22 — Holding-slot modulo is precise but not safe.**  
**Where:** P0-S §1 and §6; Day 6 duplicate-shuttle observable.  
**Class:** C.  
`queue position mod slots` can put multiple queued ships in one pose. The very observable intended to prove queueing creates the collision. **Last responsible moment:** when the second ship queues. **Replacement:** return no pose beyond authored capacity and observe whether the correct result is a line, additional generated offsets, or a visible blocked state.

**F-23 — Clock direction is needed; exact value is a knob.**  
**Where:** P0-S §0; P0-0 K03; Day 1.  
**Class:** A for the need to make movement watchable; C for `1/60` versus `1/30`.  
Keep the one serialized game-time conversion and test it immediately. Do not express later decisions in real seconds; all durations remain game-hours/game-seconds so the clock can be revisited cheaply.

**F-24 — Preserve existing facts while auditing guesses.**  
**Where:** `PLANNING_REVIEW_FACTCHECK.md`; existing `Assets/GameData/Roles/FarmOperator.asset`; `ColonistStatusComponent`; `HabitationComponent`.  
**Class:** A.  
The Farm one-worker `0.65` curve and fatigue values are shipped serialized data. They are not findings merely because they are numbers. The audit must not erase existing authorities while removing future guesses.

---

## Part B — Dependency-collapse map

| Guess/decision | Immediate consumers | What becomes blank if it is vetoed | Rework risk |
|---|---|---|---|
| Personal needs / `Fed` | Day 11 panel, Day 20 needs/death, Day 21 housing, Day 23 report, Day 25 loss screen | Need bars, personal work penalty, death cause format | **High shape change**; decide before UI and P0-D |
| Placement interaction/rules | Days 15–17, construction sites, Day 22 bootstrap | Ghost behavior, corridor completion, scenario pose representation | **High shape change**; Day 15 must remain a spike |
| Construction material/catalog | Days 14–19, logistics, scenario | Cost entries, mining target, build menu | Medium content churn; keep minimum seam |
| Allocator algorithm | Day 18 HR v2, Day 20 refill claim, Day 21 housing auto-home, Day 23 staffing report | Autonomous reassignment and related observables | Medium; manual mode can run first |
| Scenario schema | Day 22, Day 25 New Game, all authored prefab records | Hand-authored scenario asset fields | **High shape change**; capture/replay spike |
| Ledger windows/alerts/report | Days 8–11 and 23–25 | Tables, alert text, report columns | Low/medium if projections stay read-only |
| Clock value/flight magnitudes | Days 3–6, Day 12 visuals, load/unload feel, Day 19 tuning | Timing expectations and tuning | Low if all values stay game-time-authored |
| Bed authority/count | Day 2 sockets, Day 21 housing, scenario | Socket validation and housing observable | Medium prefab churn; settle authority before prefab authoring |

**Sequencing rule:** a later day may be sketched as a question, but it may not be committed to ticket depth until the previous day's expected learning is recorded. Triggers must name observable events (“after the second ship queues”), not calendar dates alone.

---

## Part C — Rolling plan skeleton

Only the first five days are specified at working depth. Every day ends with **Press Play and see** and records what was learned. A pending backlog value is a placeholder that is visibly flagged, never silently filled.

### Day 1 — Characterize and isolate
- **Work:** characterize current staffing/movement and create the minimum additive scene/assembly seam needed to let agents work without sharing `SpaceSim.unity`; expose one game-time conversion field.
- **Observable:** legacy behavior still runs from the isolated bootstrap path; compile succeeds; a short run produces the same existing duty/logistics evidence.
- **Expected learning:** which cross-scene references and singleton assumptions actually exist, and whether the clock direction makes movement observable.
- **Feeds:** scene ownership, clock revisit, staffing split. No commitment to exact scene taxonomy beyond what the experiment proves.

### Day 2 — Prefab/socket spike
- **Work:** convert one representative module and one ship, adding only sockets their next experiment requires; keep runtime authorities separate from visual slots.
- **Observable:** changing the prefab changes the scene instance without changing simulation results.
- **Expected learning:** which transform/socket categories are genuinely shared, and whether the bed/port conventions are authorable without coupling economy to art.
- **Feeds:** P0-S ports, P0-P presentation, later construction. Add categories when consumers arrive.

### Day 3 — Characterized staffing split plus isolated flight/port spikes
- **Work:** extract the first staffing responsibility behind the unchanged facade; build pure docking/flight characterization in a scratch scene, not a full voyage integration.
- **Observable:** old staffing behavior is unchanged; a test craft can expose guidance failure, overshoot, or docking ambiguity.
- **Expected learning:** which responsibilities are real seams and whether the flight model is worth continuing before committing to 6DOF magnitudes.
- **Feeds:** P0-A and the veto on flight implementation style.

### Day 4 — Route/voyage authority experiments
- **Work:** finish only the route/voyage seam needed to demonstrate one walk and one ship path; retain old callers until parity is observed.
- **Observable:** one colonist can complete an authored walk, one contract can complete a voyage, and disabling each link produces an explicit blocker.
- **Expected learning:** whether arrival/blocked semantics are legible and whether port/holding states need more than the minimal enum.
- **Feeds:** P0-A, P0-S, presentation timing. No holding-slot layout decision yet.

### Day 5 — First player-visible slice
- **Work:** camera/selection shell and one read-only inspector projection; no HR tree, report dashboard, or needs bars.
- **Observable:** the player can select one facility/ship/colonist, pause/speed the simulation, and see an existing authoritative fact.
- **Expected learning:** what the player actually looks for first and whether UI Toolkit is usable; a one-hour UI spike precedes committing to the stack.
- **Feeds:** UI structure and the next three-day window.

### Rolling rule after Day 5
Each next window contains at most 3–5 days. A day has: **work, observable, expected learning, backlog questions it can answer, and a stop condition**. If the learning contradicts an upstream guess, the next day is a fix or re-spike, not the next numbered feature. Days 14–26 remain horizon questions until their triggers fire.

---

## Part D — Decision backlog

The human is the decision-maker for every row. “Trigger” is an observable event, not a date.

| ID | Question | Trigger / experiment | Current blank | LRM |
|---|---|---|---|---|
| B-01 | Personal needs or aggregate shortage? | A shortage occurs and the player wants to know who is hungry | No personal owner/API | Before needs UI |
| B-02 | What warning/death shape? | Watch a shortage run with debug consequence logging | No death timer | After first playable |
| B-03 | How should impaired workers affect output? | One impaired worker is visible at a staffed facility | No aggregation semantic | Before work penalty |
| B-04 | What is homeless restfulness? | First real bed shortage is observed | Multiplier pending; runtime seam retained | Housing playtest |
| B-05 | Snap, free placement, or both? | First placement ghost is dragged | Placement mechanism blank | Placement spike |
| B-06 | Are planes needed beyond one site? | A second physical site is requested | No `planeId`/cross-plane rules | Second site |
| B-07 | Which first build material? | First construction site requests a cost | Minimum resource only | First build job |
| B-08 | Which buildings are player-buildable? | Player places first and second build jobs | Catalog blank beyond required entries | Build menu spike |
| B-09 | Player chooses deposits or controller ranks them? | Two deposits are visible | Existing single target preferred | Two-deposit playtest |
| B-10 | What allocator behavior is wanted? | Manual HR has survived a shift change | No release/hysteresis/throttle policy | Before allocator |
| B-11 | What is the smallest scenario schema? | A hand-built base successfully replays | Capture/replay output is the candidate schema | Scenario bootstrap |
| B-12 | What report answers a loss? | First loss cannot be explained from the shell | One plain-language explanation only | First loss |
| B-13 | What alert thresholds? | A normal run provides metric distributions | Thresholds empty/pending | After first 72h observation |
| B-14 | How many holding poses and what happens beyond them? | Second ship queues | No modulo placement rule | Queue spike |
| B-15 | UI Toolkit or uGUI? | One-hour shell spike completes | Stack not locked by prose | Before six panels |
| B-16 | Interior visibility and module kit | First colonist must be noticed working/sleeping | Art/view strategy blank | Before presentation pass |
| B-17 | Colonist identity depth | First death feels anonymous | Names/traits/portraits blank | Before emotional polish |
| B-18 | Is the Water Processor automated in the slice? | First staffing/logistics trade-off is watched | Content toggle pending | Before scenario lock |
| B-19 | How much of exploration is slice input? | First player asks where the next resource is | Horizon only; no Phase 1 content commitment | Phase 1 planning |
| B-20 | Which constitution rules are proposals vs proven invariants? | Human review of the architecture register | One canonical list, no duplicated summary | Before dispatch |

---

## Part E — De-specification pass for worst offenders

### P0-D: Live & Die
Replace the current draft with three seams and questions:

- A needs observation seam that can consume the existing aggregate shortage without inventing `Fed`.
- A consequence/debug spike that records need curves and warning transitions; death is gated until the owner has watched a shortage.
- `Kill(colonist, reason)` as an invariant-checked command; no mandated order beyond “all holders cleared, history recorded, no duplicate cleanup.”
- Housing keeps runtime `capacity`; a visual socket list is not the authority. Auto-homing and homeless multiplier remain backlog questions.

### Day 15 / P0-C: Placement
Keep only a pure placement evaluator, a ghost, and the observable “confirming a valid pose creates a site.” Start with the result states the ghost demonstrates. Move rotation snapping, node radius, corridor geometry, plane identity, free-placement fallback, and lock/unlock semantics to B-05/B-06.

### Day 22 / P0-D: Scenario
Keep “New Game uses the same creation/completion path as construction.” Replace the hand-authored fifteen-field schema with a capture/replay spike and add fields only when the round-trip fails without them.

### Day 23 / P0-B: Report
Keep read-only ledgers and one plain-language explanation of a blocked/failed state. Remove mandated tables, sparklines, Top(20), four threshold values, and exact sentences until a first loss or blocked run demonstrates what cannot be understood.

### Day 18 / P0-C: Allocator
Ship target/priority/pin data and a manual HR command first. Do not ship autonomous reassignment, release order, hysteresis, or “allocator changed” presentation until manual play creates the question those features answer.

---

## Part F — Protect list

Do not throw these away while de-specifying:

- Inventory remains the sole quantity authority; discrete quantities stay normalized at boundaries.
- Converters never count workers; performance providers expose operational state and effects.
- Explicit employment remains distinct from the temporary pilot lease.
- `currentLocation` changes only on arrival; transit state and presentation remain separate.
- Publish-then-commit logistics and reservation only after a vehicle wins arbitration.
- Extraction stays outside freight arbitration.
- One-way dependency direction and read-only views/narrow validated commands.
- Serialized runtime-authoritative state, registries on hot paths, explicit enum state, and pause/disable semantics.
- Characterize before moving code; preserve a facade while splitting the staffing hotspot.
- Additive scene ownership to prevent parallel-agent conflicts.
- Authored-link walk/shuttle distinction and visible blocked reasons, subject to observed tuning.
- One voyage authority as the response to three existing hand-rolled movement callers, subject to flight-model experimentation.
- Read models as projections, subject to schema discovery.
- The daily “Press Play and see” gate, strengthened with “what we expect to learn.”
- Owner decisions already explicitly recorded as owner decisions, especially the hybrid control intent and the strategic importance of corridor versus shuttle. Do not reopen them merely because the implementation details are blank.

## Definition-of-done check

A revised plan passes this audit when a reader can answer:

1. The next three days are concrete and each says what it expects to learn.
2. Every later decision is either a verified fact, a necessary seam with a trigger, or an explicit blank in the backlog.
3. No agent can silently fill a pending value: tickets say “pending backlog item; ship flagged placeholder.”
4. Triggers are observable events, not calendar promises.
5. The owner can veto any proposed choice without first unpicking a downstream schema, panel, or content catalog.

**Final verdict:** keep the skeleton and the protect list; demote the invented answers; make the rolling learning loop load-bearing. The plan becomes trustworthy when its blank spaces are visible enough that the next agent cannot mistake them for permission to design.
