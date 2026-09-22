# Muse Spark — Adversarial Review: The Blank Space Audit

> **Prong:** Muse Spark (1 of 3 parallel adversarial reviewers per `PROMPT_ADVERSARIAL_PLAN_AUDIT.md`)
> **Plan under review:** Fable's preliminary planning review, commit `75e5784` (banner-marked TENTATIVE), as checked in `PLANNING_REVIEW_FACTCHECK.md` (2026-09-20, verified true against codebase at `c2b92cc`)
> **Date:** 2026-09-20
> **Where to write:** this file (`musesparkadversarialreview.md`) — synthesis input for owner
> **Stance:** Loyalty is to the owner's working method, not to the plan. A blank is a valid output. Precision is not justification.

The owner, in his own words:

> "I want to make intentional bespoke decisions, and discover things through development and testing, rather than make guesses ten steps down the road. AI models often paint 'app-shaped bullshit' into a blank space and I don't want that — I want a space to stay blank until we put the right thing there."

Fable's plan is mechanically **accurate** (every greppable claim survived the fact-check) and still **prematurely committed**: 26 days of pre-decided outcomes for systems nobody has touched, felt, or played. Every unearned decision is debt the owner pays later by living with something he never chose or paying to un-specify it.

This review does not grade the plan. It hunts every place it decides things nobody currently knows enough to decide, and every place a space stayed blank only until something was painted into it. Every finding is VETO-ABLE — never "wrong." The human vetoes; agents propose. Fable's reasoning trail is preserved so vetoes are informed.

---

## Method (as executed)

Read order prescribed by the prompt: `PLANNING_REVIEW_FACTCHECK.md` → `HOW_IT_WORKS.md` → `GAME_DESIGN_DECISIONS.md` + `ARCHITECTURE_CONSTITUTION.md` → `DAY_BY_DAY_PLAN.md` → packets in dependency order `P0-0`, `P0-A`, `P0-S`, `P0-B`, `P0-P`, `P0-C`, `P0-D`, `P0-E`, `P1-X`. Skimmed real code only to distinguish class **A** (forced by existing code) from **D**. For each ticket/day extracted decisions and classified A/B/C/D. For each C/D named missing information, experiment/milestone that produces it, last responsible moment, and minimal seam.

Classification:

- **A — Forced.** Mechanical consequence of existing code or owner intent. Keep.
- **B — Needed now.** Downstream genuinely blocked without it; no experiment could inform it first. Keep with revisit trigger.
- **C — Decidable later.** Blank it. Specify only seam/interface, question, experiment, last responsible moment.
- **D — App-shaped bullshit.** Invented because planning AIs fill blank space — invented constants, schemas, UI, balance. Delete from tickets; move to backlog as open question.

---

## Executive Verdict (one paragraph)

The plan earns confidence where it describes **today** and loses it where it designs **Day 14–26**. The staffing split (Epic G), the transit seam (Epic H), the voyage authority, the ledger read-models, and the additive-scene split are forced by real seams in real files and should survive verbatim. Everything that assigns a number, a schema, a panel layout, or a content list to a system that has never been played is C/D debt — about 60–70% of the prose in `DAY_BY_DAY_PLAN.md` Days 14–26 and `P0-C`/`P0-D` drafts, ~40% of `P0-S` `01_LOCKED_DESIGN.md`, and the "locked" framing in `GAME_DESIGN_DECISIONS.md`/`ARCHITECTURE_CONSTITUTION.md`/`DECISION_LOG.md`. The fix is not to replace guesses with my guesses — it is to **blank them**, keep the seams that make work runnable, and re-hang the guesses on trigger-gated questions the human answers after play.

Fable credited for: flagging `P0-C` and `P0-D` as **DRAFT, lock after Day 13 / Day 6**, adding "Vetoes requested" in `DECISION_LOG.md`, and writing a per-day "Press Play and see X" — all are exactly the instincts this audit extends to *everything*.

---

## Protect-List — What Is Genuinely Good and Load-Bearing (do not de-specify)

These are class **A** facts. Protect them; the de-specification must not throw them out.

**1. Codebase invariants proven by grep/tests (§ HOW_IT_WORKS §3, STATE_OF_THE_PROJECT, FACTCHECK C1–C7, D1–D5):**
- `InventoryComponent` is sole quantity authority (`onHand`/`reserved`/`capacity`, discrete normalization at every boundary). Racks/panels are views.
- `ResourceConverterComponent` never counts workers; `FacilityPerformanceComponent` aggregates `IFacilityPerformanceProvider` channels (`IsOperational`, `GetMultiplier`).
- Explicit employment: `EmploymentAssignment` on colonist, mutated only via `StaffingManager.Assign/Unassign → AssignmentResult`. No auto-vacancy-filling, no call-ins.
- Pilot lease: `ShipComponent.ResponsiblePilot` is a temporary lease distinct from employment.
- Publish-then-commit logistics: `ResourceStockPolicyComponent` publishes `FreightDemand`/`FreightSupply`; `ContractManager`/`LogisticsManager` arbitrate; reservation only **after** a vehicle wins. Keeps tiny pointless shipments from happening.
- Extraction deliberately outside freight arbitration (`ExtractionMissionController`).
- `ColonistAgent.currentLocation` is last *arrived* anchor; transit origin/destination are separate; "arrival means arrived" is real in code (`BeginTransit`/`CompleteTransit`).
- `SimulationManager` tick registry with priorities `100 → 200 → 300 → 400 → 900`; `Update`/`FixedUpdate` are presentation-only; pause/speed via `SetPaused`/`SetSpeedMultiplier`.
- No player input/camera/selection exists today (verified `Canvas`/`UIDocument`/`Input.`/`Raycast` 0 hits) — so building them is indeed needed.
- Scene is a fixed diorama; no docking ports/queueing (single `Transform dockingPort`), flight is 23-line `Vector3.MoveTowards` with 3 callers. Those seams are real gaps.

**2. Seams the split correctly cuts (P0-A `01_LOCKED_DESIGN.md` Part 1):** `StaffingManager` is 1,063 lines / 55 methods — extracting `EmploymentRegistry`, `CommuteBatcher`, `ColonistReconciler`, `PilotDutyReconciler`, `ScheduleReportFormatter` behind an unchanged facade is forced by file size and writer contention (allocator + walking both write duty state). Keeping the facade, keeping one writer for `ColonistDutyState`, and enumerating registries not scene scans on hot paths are A.

**3. Strategic core as seam, not balance (GAME_DESIGN_DECISIONS § corridors vs shuttles, HOW_IT_WORKS §2):** "RouteResolver: walk if corridor path exists, else ship, else Blocked — no third option — runtime never balances it; layout and content do." The *mechanism* is A/B (it creates the game's decision). Any *number* attached to it (cost, time, length) is C/D.

**4. Additive scene split + asmdef direction (P0-0 K01–K03, PACKET_INDEX, Constitution Rules 1–2):** `Content ← Runtime ← Presentation ← UI`, runtime never referencing presentation/UI, views read-only, `Deleting every view leaves sim identical` — these are architectural invariants worth locking. The observation that everything editing one `SpaceSim.unity` blocks parallel agents is real (F1); the fix (Bootstrap + Managers/Base/Environment/UI) is B.

**5. Discovery mechanism (owner explicitly likes):** Deciding each day's work one day ahead and ending each day with "Press Play and see X." Keep and strengthen: each day should also state **what it expects to learn**. The cadence must feed learnings forward, not execute a frozen script.

---

## Audit Findings — Numbered, Ruthless, Specific

Every finding: decision + where it lives + class + missing information + last responsible moment + replacement (seam/spike/blank). Mark is VETO-ABLE.

### Seed categories mapped to file+day/ticket (prompt §4)

#### 1. Invented Constants — numbers that feel like design but are guesses

**F-001 — Death timers: hydration 0 for 24h, nutrition 0 for 72h → death**
- Where: `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` §Needs (Day 20); `DAY_BY_DAY_PLAN.md` Day 20; `GAME_DESIGN_DECISIONS.md` §People
- Class: **D** — no system exists to feel hunger; no play has informed lethality
- Missing: How long should starvation take to *feel* threatening without being tedious? Depends on clock, walk times, freight latency — all unbuilt.
- Last responsible moment: When a colonist can starve in a real 24h playtest with freights running (after Day 19 integration).
- Replacement: Seam `PopulationManager.Kill(colonist, reason) → KillResult` stays (unwind order is B — needed for teardown). Thresholds become **blank** + backlog Q: "What lethality makes over-building feel consequential?" Spike: run invulnerable colonists first, log `nutrition`/`hydration` curves for 72h (see de-spec ticket D20′).

**F-002 — Homeless restfulness 0.5**
- Where: `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` §Housing (Day 21); `HOW_IT_WORKS.md` §2 (build habitat, beds appear); `GAME_DESIGN_DECISIONS.md` §People
- Class: **D**
- Missing: Is homelessness a soft penalty or a hard block? Depends on sleep/bunk presentation (undecided, GAPS open: cutaway vs windows) and bed art scope.
- LRM: After first Habitat is built and rest is observed with 8 colonists/6 beds (Day 21 playtest).
- Replacement: Keep `HabitationComponent` capacity = `ModuleSockets.beds.Count` as seam (count is B). Blank the multiplier; expose `restfulnessMultiplier` as authored per housing type in backlog.

**F-003 — Fatigue: +0.10/h working (× exertion), −0.10/h sleeping (× restfulness), latch 0.90→0.20**
- Where: `GAME_DESIGN_DECISIONS.md` §People; `P0-A` fatigue language; `HOW_IT_WORKS.md` §2
- Class: **C** — values are plausible but informed by 24-second day that no longer exists (FACTCHECK D2). Clock retune invalidates them.
- Missing: How fatigue *feels* at 60× slower clock + walking vs flying commutes.
- LRM: After Day 6 corridor/shuttle split is playable; observe exhaustion frequency.
- Replacement: Keep `ColonistStatusComponent` fatigue + `IFacilityPerformanceProvider` channel as seam. Blank constants; backlog Q with trigger "decide when you've watched your first 2 shifts with walking."

**F-004 — Farm production curve "0.65 for one tech" and ConstructionRate curve 0/0.6/1.0/1.3**
- Where: `HOW_IT_WORKS.md` §2 (0.65), `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` §Content (ConstructionRate)
- Class: **D**
- Missing: Is 8 colonists with 2 farm techs sustainable? Depends on consumption, freight, and bed pressure — all un-played.
- LRM: Day 19 economy tuning 72h run.
- Replacement: Keep `FacilityEffectDefinition` curve as seam (content-owned). Blank the numbers; file as "balance hypothesis, tune at Day 19 spike." No curve in the ticket.

**F-005 — Rotation snapping 15° (Placement)**
- Where: `DAY_BY_DAY_PLAN.md` "Locked on the spot"; `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` §Placement
- Class: **D** — AI painted a number into "what do you place buildings ON?" The owner called this exact sentence the disease: "Locked on the spot" — words "locked" and "on the spot" in same sentence.
- Missing: What snapping *feels* right for base layout? Depends on node distances, corridor max length, art footprint — all placeholder primitives today.
- LRM: After `ModuleSockets.attachmentNodes` exist on prefabs (Day 2) and ghost is draggable (Day 15 first playable).
- Replacement: Seam `PlacementRules.Evaluate(def, pose, plane) → PlacementResult` with no constant; blank snapping into backlog Q "free vs node-snapped vs 15° vs 45°" triggered by "first time you drag a habitat ghost."

**F-006 — Alert thresholds: food <12h, colonist blocked >2h, port holding >1h, site starved >8h**
- Where: `DAY_BY_DAY_PLAN.md` Day 23; `tickets/P0-B_Interaction_UI/01_LOCKED_DESIGN.md` §7
- Class: **D** — invented triage UI before any blocking has been observed
- Missing: Which alerts actually matter to a player? Depends on freight latency at new clock.
- LRM: After first blocked/shuttle queue is seen (Day 6) and ledgers exist (Day 9).
- Replacement: Keep `BlockedTimeLedger` / `ResourceFlowLedger.HoursUntilEmpty` as seams. Blank thresholds; backlog Q per alert, trigger "when you've been annoyed by the lack of an alert."

**F-007 — Starting stock "48h of food"**
- Where: `DAY_BY_DAY_PLAN.md` "Locked on the spot"; `GAME_DESIGN_DECISIONS.md` §Starting scenario; `HOW_IT_WORKS.md` §intro
- Class: **D**
- Missing: Does 48h give a meaningful grace period or trivialize the Farm? Depends on consumption + farm output at new clock.
- LRM: Day 19 tuning.
- Replacement: `ScenarioDefinition.startingStock[]` seam stays; amount blank.

**F-008 — Starting roster 8 colonists split 3 Pilots / 2 Farm Techs / 3 Builders**
- Where: Same as F-007
- Class: **D** — 3/2/3 is app-shaped crew math before any staffing target has been set by a player
- Missing: What scarcity makes the corridor vs shuttle choice meaningful? Depends on HR/targets UX (unbuilt).
- LRM: After allocator exists (Day 18) or manual HR first use (Day 11).
- Replacement: Keep `colonists[] {classes, skills, home, employment?}` as seam. Split blank.

**F-009 — Corridor `traversalHours = 0.25` (15 min) and generic link traversal cost**
- Where: `tickets/P0-A_Foundation/01_LOCKED_DESIGN.md` TransitLinkComponent; `P0-D` habitat/bed math
- Class: **C** — authored walking cost is needed to test walking, but 0.25 is a guess
- Missing: How long should a cross-base walk take vs a shuttle flight at new clock + substepped integrator?
- LRM: After Day 6 both modes side-by-side are observable.
- Replacement: Keep `traversalHours` field as seam (content decides). Value blank / hypothesis; backlog Q triggered by "watch first walk vs first flight."

**F-010 — Clock: `gameHoursPerRealSecond = 1/60`, `tickInterval 0.1`, `substep 0.5s`, `maxSubsteps 200`**
- Where: `DAY_BY_DAY_PLAN.md` Day 1 / F11; `tickets/P0-S_Shuttle_Flight/01_LOCKED_DESIGN.md` §0 + §2; `P0-0` K03
- Class: **B/C mix** — slowing the 24-second day *is* needed (B, nothing watchable at 1h/s — FACTCHECK D2). The exact 60×, and integrator 0.5/200, is C — unproven at new transport speeds.
- Missing: What speed still looks smooth with substepped Newtonian flight and pedestrian walks?
- LRM: Day 4 substepped integrator observable; Day 6 flight + walk side-by-side.
- Replacement: Keep `gameHoursPerRealSecond` as seam with revisit trigger; blank 1/60 as "hypothesis: 30–60s per game hour." Integrator numbers become a spike experiment, not a lock.

**F-011 — Flight profile numbers: mass 20000, mainThrust 60000, RCS 8000, torque 40000, inertia 150000, cruise 40 m/s, approach 3, push 15, etc.**
- Where: `tickets/P0-S_Shuttle_Flight/01_LOCKED_DESIGN.md` §2 ShipFlightProfile
- Class: **D** — exhaustive physics constants for a ship never flown by a player
- Missing: What 2000 m hop should take (60–120 game-seconds placeholder) depends on station spacing the player chooses — unknown.
- LRM: Day 4 voyage authority first flight + Day 6 port queue.
- Replacement: Keep `ShipFlightProfile : ScriptableObject` + `FlightIntegrator`/`FlightGuidance` as seams. Blank numbers; ticket S02 becomes "report hop times for 3 thrust values, choose later."

**F-012 — Timed loading: 0.002 h/unit (≈7 game-s) and 0.01 h/passenger**
- Where: `tickets/P0-S_Shuttle_Flight/01_LOCKED_DESIGN.md` §4
- Class: **D**
- Missing: Does loading need to be watchable vs freight throughput?
- LRM: After first freight contract completes via voyage (Day 5).
- Replacement: Keep `TransferProgress01` + "loading takes game-time" as seam. Blank rates.

**F-013 — Needs consequences: `lerp(0.4, 1.0, min(nutrition,hydration))` for fatigue recovery and work multiplier**
- Where: `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` §Needs
- Class: **D** — dressed guess mid-sentence
- Missing: How punitive hunger should be before death.
- LRM: After needs are observable (Day 20 first bars).
- Replacement: Keep colonist-level `WorkEffectiveness` multiplier seam (existing `IFacilityPerformanceProvider` reuse). Blank curve; backlog Q.

**F-014 — Stock policy volumes: "keep Water above 20, refill to 60"; Processor "export above 10"; etc.**
- Where: `HOW_IT_WORKS.md` §2 walkthrough (01:00 vignette); `P0-C` CommandPod "target 60"
- Class: **D** — narrative filled with numbers that will anchor expectations (see §8 deletion of blank)
- Missing: What threshold avoids thrashing with new freight?
- LRM: After logistics arbitration is seen at new clock (Day 5 onward).
- Replacement: Keep stock policy seams (foreground/background, threshold/target/priority). Blank volumes; they are scenario content.

**F-015 — Construction: `priority 8` for site import, `workPriority 1–10`, `evaRange`, `maxCorridorLength`**
- Where: `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` §Construction site, §Placement
- Class: **C/D**
- Missing: Does priority 8 starve food freight? E.g., regolith vs water contention.
- LRM: After first competing demands (Day 16 first site).
- Replacement: Keep `ResourceStockPolicy.priority` and `workPriority` as seams. Blank levels.

#### 2. Invented Entities — types/machines specified to field level before design

**F-016 — `ScenarioDefinition` full field list (`sitePlanes[]`, `modules[] {pose, planeId, initialStock[], staffingTargets[]}`, `corridors[] {moduleA, nodeA, moduleB, nodeB}`, `ships[] {dockModule, portIndex, flightProfile}`, `deposits[] {prefab, position, amount}`, `colonists[] {name, classes, skills, home, employment?}`, `startHour`, `clockDefaults`)**
- Where: `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` §Scenario (Day 22); `DAY_BY_DAY_PLAN.md` Day 22
- Class: **D** — invented schema for a `ScenarioBootstrap` that consumes Day 14–17 shapes that don't exist; F13 names this dependency.
- Missing: What bootstrap should build through — the *actual* completion path of construction/corridors/ports — which won't be known until Days 16–17 land.
- LRM: After corridor creation path and prefab sockets exist (Day 17).
- Replacement: Seam: `ScenarioDefinition is a ScriptableObject that spawns via construction completion paths` (one sentence). Field list blank; ticket D-C becomes spike: "what does bootstrap need to call?" Backlog Q.

**F-017 — `PopulationManager.Kill()` 8-step unwind order (end duty → unassign → remove from passenger lists/carriers → cancel pedestrian transit → clear ResponsiblePilot → history → unregister → destroy, with log-and-continue)**
- Where: `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` §Death; `DAY_BY_DAY_PLAN.md` Day 20 / F12
- Class: **C** — order *matters* (F12), but the 8-step list assumes systems (pedestrian, carrier, voyage) that haven't landed; today's `FindObjectsByType` in staffing is still hot (FACTCHECK D3).
- Missing: Actual teardown failure modes when a colonist dies mid-walk vs mid-flight vs queued.
- LRM: After housing + pedestrian + voyage exist (Day 13 + Day 6).
- Replacement: Seam `Kill(colonist, reason) → KillResult` with requirement "unwind in dependency order, log and continue." Steps blank; ticket becomes spike enumerating real failure cases.

**F-018 — `WorkforceAllocator` tick priority 90, hysteresis 4h, throttled to once/game-hour, `workPriority` sort, pin skip, badge**
- Where: `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` §Staffing targets; `DAY_BY_DAY_PLAN.md` Day 18; `ROADMAP.md` §C
- Class: **D** — full allocator design before manual HR has been used
- Missing: Does the allocator thrash at shift boundaries? What hysteresis feels right? Depends on duty-phase writes (just split in P0-A).
- LRM: After HR v1 manual assignment has been used (Day 11) and reconcilers are split (Day 4).
- Replacement: Seam `EmploymentRegistry.GetAssignmentCandidates/Assign` as allocator client; blank priority/hysteresis; backlog Q per constant.

**F-019 — Ledger schemas: `ResourceFlowLedger` hourly buckets over rolling 24h with `In24h/Out24h/NetPerHour/HoursUntilEmpty` + `GetColonyTotal`; `BlockedTimeLedger` open intervals on `*.blocked`, `ship.phase`, `duty.*` with `HoursBlocked24h/Top(n)`; `DutyReportBuilder` sentence format "worked 5.2h of 8h, left Exhausted at 14:20"**
- Where: `tickets/P0-B_Interaction_UI/01_LOCKED_DESIGN.md` §4; `DAY_BY_DAY_PLAN.md` Day 9 / F9/F10
- Class: **C/D** — buckets/rolling window/sentence template are invented for systems never aggregated; F9/F10 seam is real (inventory change feed exists as `OnChanged`; history `Record` exists) but the aggregation shape is guess.
- Missing: What players actually need to diagnose "Farm produced nothing this shift because Water was empty for 6h" (GAPS).
- LRM: After first time a player asks "why did Farm stop?" and ledgers are needed (Day 10 panels).
- Replacement: Keep `InventoryComponent.OnChanged` and `ReadinessHistory.OnRecorded` event as seams (B — minimal UI seam added Day 8). Blank ledger shape; ticket becomes spike: subscribe, log, see what questions arise, then define minimal read model.

**F-020 — `HabitationComponent` auto-homing + `PopulationManager.SetHome` homelessness**
- Where: `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` §Housing (Day 21)
- Class: **D** — adds automation (auto-home homeless) before manual housing UX exists
- Missing: Should beds auto-assign? What about pinned colonists? Depends on identity depth (GAPS open: names/portraits/traits).
- LRM: After HR shows homeless count (Day 18) and Habitat is first built.
- Replacement: Seam `HabitationComponent.capacity = beds.Count` + `SetHome` command; blank auto-homing; backlog Q "auto vs manual homing."

**F-021 — Docking spec: `DockingPortComponent` + `DockingControlComponent` grant policy (priority desc → requestedHour asc → registration order), `captureDistance 0.3`, `captureSpeed 0.5`, `captureAngle 5°`, holdingSlots mod, adopt `Occupied` in Start, startup validation demanding ≥1 port per anchor**
- Where: `tickets/P0-S_Shuttle_Flight/01_LOCKED_DESIGN.md` §1
- Class: **C/D** — finite ports + hold-if-busy is A/B (strategic build decision, owner requirement). Every geometric/queue constant is D.
- Missing: How close a 12 m shuttle should get before capture feels right at new clock + HDRP port lights.
- LRM: After first berth request is visible (Day 5) and port views exist (Day 13).
- Replacement: Keep `RequestBerth → Granted|Queued|Denied` + `Holding` phase as seam. Blank capture numbers; they become tuning hypotheses.

**F-022 — Voyage phase machine: 9 `VoyagePhase` values, `VoyageDestinationKind`, `VoyageRequestResult` with 6 cases, `TryAbortToNearestBerth`, `BerthDenyReason`, `Reserved`/`Occupied` ports**
- Where: `tickets/P0-S_Shuttle_Flight/01_LOCKED_DESIGN.md` §3
- Class: **C** — authority + phases Docked→Undocking→Cruise→Request→Holding→Approach→Final→Docked/Loiter/Blocked is needed to unify 3 callers (B — real hotspot: 3 components hand-roll). Full enum + abort affordance is overspecified.
- Missing: Which phases players need to see vs internal transitions; whether abort should auto-return.
- LRM: After S03 `ShipVoyageComponent` compiles and S04 caller migration is attempted.
- Replacement: Keep `ShipVoyageComponent` as one writer of `ShipMovementPhase` + `RequestVoyageToBerth/Loiter` commands as seam. Blank enum list beyond ~5 core phases; add phases only when callers demand them.

**F-023 — `BuildingDefinition` entries + `TransitLinkComponent` definition**
- Where: `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` §Content; `P0-A` §TransitLinkComponent
- Class: **D** — "Corridor / Habitat / Farm / Water Processor / Dock Port Module" as the closed list, with cost `Regolith` and `Builder` role, is content chosen before any building has been placed
- Missing: What buildings make the slice *feel* like Banished? Depends on art kit (GAPS: module kit has no owner) and interior visibility.
- LRM: After placement ghost is draggable (Day 15) and first site completes.
- Replacement: Seam `BuildingDefinition` type exists + `TryPlace` → `PlacementResult` (B). Entry list blank; backlog Q per building/content.

**F-024 — `SitePlane` (origin+normal, planeId) + EVA temporary `TransitLink`**
- Where: `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` §Placement/§Construction site; `EXPLORATION_AND_LONG_RANGE.md` §8; `DAY_BY_DAY_PLAN.md` Day 15–17
- Class: **B/C** — `SitePlane` avoiding hard-coded y=0 is B (second-act seam: outposts need it). `temporary` EVA link tagged vs "EVA port" vs shuttle-unload-at-nearest-module is C — alternatives not weighed (invented preference).
- Missing: How builders reach a site with no corridor and no port.
- LRM: After first site is placed off-corridor (Day 16).
- Replacement: Keep `SitePlane` as seam with `Never hard-code y=0` rule. Blank EVA mechanism; ticket spike tries both options and records which feels better.

#### 3. Invented UI — panel layouts, columns, hotkeys before play

**F-025 — HR v1/v2 layout: left colonists table (name/classes/employment/state/fatigue), right workplace tree (workplace→role→shift rows with assigned/cap, candidates list with AssignmentResult reason, Assign/Unassign, filters eligible/blocked/off-shift), v2 adds target ±, priority slider, pin toggle, allocator on/off + badge**
- Where: `tickets/P0-B_Interaction_UI/01_LOCKED_DESIGN.md` §6; `02_TICKETS.md` U06
- Class: **D** — detailed HR UX before any colonist has been assigned through UI (current `Assign` is Inspector-only per `STAFFING_AUTHORING.md`)
- Missing: What assignment failures players actually hit; what columns answer "why can't I staff this?"
- LRM: After U05 colonist panel exists and manual `Assign` has been used (post-Day 11).
- Replacement: Seam: HR screen calls `EmploymentRegistry.GetAssignmentCandidates/Assign/Unassign` and shows result text. Layout/columns blank; spike: paper prototype after U06 manual play.

**F-026 — Colony Report sections + table columns: Population/ Resources (onHand In24h Out24h Net/h HoursUntilEmpty sparkline) / Blocked time Top(20) subject/reason/hours/last / Duty failures sentences / Transport completed/cancelled/avg wait/holding per port**
- Where: `tickets/P0-B_Interaction_UI/01_LOCKED_DESIGN.md` §7; `DAY_BY_DAY_PLAN.md` Day 23
- Class: **D** — consumes ledger fields (F-019) that don't exist
- Missing: Which table actually explains a death or a starvation? GAPS gap: per-facility "shift summary" sentence.
- LRM: After ledgers + panels have been used to diagnose a 48h run (Day 10–11).
- Replacement: Keep `DutyReportBuilder.sentences` idea as question: "plain-English duty summary." Columns blank; backlog Q per section triggered by "need to explain X."

**F-027 — HUD strip contents: day/hour-of-day, pause, 1×/2×/4×/10×, population, food/water on hand, alert toast list fed by `OnRecorded` tail**
- Where: `tickets/P0-B_Interaction_UI/01_LOCKED_DESIGN.md` §3; `02_TICKETS.md` U01; `DAY_BY_DAY_PLAN.md` Day 8
- Class: **C/D** — HUD at Day 8 before any read model exists; "population, food/water on hand" is B (needed to see survival), but full toast allow-list is D.
- Missing: What deserves a toast vs a ledger row?
- LRM: After first toasts are seen (Day 8) and alert spam is felt.
- Replacement: Seam `ReadinessHistory.OnRecorded` event + day/hour readout (B — minimal). HUD fields beyond time + pause/speed blank until needed.

**F-028 — Hotkeys: Space pause, 1–4 speed, F focus, Esc menu**
- Where: `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` §Session frame (Day 25); `DAY_BY_DAY_PLAN.md` Day 25
- Class: **D** — consumes Day 7 camera + Day 8 time controls before either exists
- Missing: Which keys players reach for at 10× with orbit camera.
- LRM: After camera + time controls exist (Day 8).
- Replacement: Seam: Input System actions asset. Bindings blank; decide after first 15-min playtest.

**F-029 — Selection: `SelectableKind {None,Facility,Colonist,Ship,Deposit,Port,ConstructionSite}` single `SelectionModel`, raycast ignoring Environment, highlight on mesh root**
- Where: `tickets/P0-B_Interaction_UI/01_LOCKED_DESIGN.md` §2; `02_TICKETS.md` U00
- Class: **C** — selection *is* needed to see anything (B for kind existence), but 7-kind enum + single-selection is D (what about multi-select? port selection before ports exist?)
- Missing: Does selecting a transit link or a site matter before those exist?
- LRM: After P0-S ports and P0-C sites exist.
- Replacement: Keep `SelectionModel.Current` + `ISelectable` marker as seam. Kind list blank beyond ~4 (Facility/Colonist/Ship/Deposit); add kinds when their packet lands.

**F-030 — Presentation composition: `ShipView` tick interpolation over `tickInterval/speedMultiplier`, `ThrusterSet` mapping, `DockingPortView` lights (Free green/Reserved amber/Occupied blue/Closed red), `HoldingPatternView` beacon, `VoyageTrailView` ribbon, `ColonistView` spline, `FacilityPresenter` workstation/bed slots, `SlowTumble 0.5–3°/s`**
- Where: `tickets/P0-S_Shuttle_Flight/01_LOCKED_DESIGN.md` §5; `tickets/P0-P_Presentation_People/00_README_AND_TICKETS.md` §Design; `ENVIRONMENT_ASSETS.md` §Art bible
- Class: **C/D** — interpolation/thrusters/port lights are B (ship is what player watches per `GAME_DESIGN_DECISIONS.md` Identity). Exact light colors/beacon text/trail as D.
- Missing: What looks right at 10× with substepped flight and HDRP volumetrics?
- LRM: After ship actually flies (Day 4) and dust is visible (Day 24).
- Replacement: Keep `ShipView` interpolates child mesh root, reads `Flight.commanded*` as seam. Visual details blank; tuning hypotheses.

#### 4. Invented Content

**F-031 — `Regolith` as the single construction material (Discrete), `RockDeposit` + rule "no fabrication chain in slice"**
- Where: `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` §Content; `DAY_BY_DAY_PLAN.md` "Locked on the spot"; `ROADMAP.md` §B
- Class: **C** — some material is needed to make construction request something (B), but anointing `Regolith` + "no chain" as the material is C — alternatives (generic `BuildingMaterial` vs ore→metal) not weighed (GAPS open: resource chains beyond Regolith).
- Missing: Does mining Regolith duplicate the ice loop or create a meaningful second deposit choice (F6: pick among authored deposits)?
- LRM: When `BuildingDefinition.cost` seam exists (Day 14 extraction controller).
- Replacement: Seam `cost: ResourceAmount[]` on site inventory. Resource identity blank; backlog Q "one vs two materials in slice" triggered by first freight run with two deposits.

**F-032 — Farm's production curve and `Builder` role `ConstructionRate` effect (exertion 1.2)**
- Where: `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` §Content + `HOW_IT_WORKS.md` §2
- Class: **D** — see F-004

#### 5. Invented Preferences — presented as settled, no alternative weighed

**F-033 — Water Processor automated (no staffing role) so it demonstrates shuttle trade-off through freight alone**
- Where: `GAME_DESIGN_DECISIONS.md` §Starting scenario; `HOW_IT_WORKS.md` §2 (processor automated — 1.0); `DECISION_LOG.md` #27
- Class: **C** — avoids spending a colonist, but bakes "automation as toggle" into the slice before staffing taxonomy is tested; `EXPLORATION_AND_LONG_RANGE.md` §8 reserves `operatingRole == null` to mean automated with no code — D if enforced too early.
- Missing: Does automating it hide the staffing-vs-shuttle decision the slice is meant to demonstrate?
- LRM: After allocator + targets exist (Day 18) or after watching first Water Processor shift with a staffed alternative (spike).
- Replacement: Seam `operatingRole == null` reserved meaning as B. Automated vs staffed Water Processor blank; backlog Q with experiment "staffed processor for one shift."

**F-034 — Hybrid target+allocator staffing (targets per role×shift + priority, pinning, manual fallback)**
- Where: `GAME_DESIGN_DECISIONS.md` §Control model; `ROADMAP.md` §C; `P0-C` §Staffing targets
- Class: **C** — owner chose hybrid per `DECISION_LOG.md` #6 (counts as B-for-intent), but the *shape* (per-role-per-shift target grid + 1–10 priority + pin) before manual HR is D. The axiom "targets + priority" painted over the blank "how should staffing control feel?"
- Missing: How manual assignment *feels* before deciding where automation helps (F8: HR v1 before allocator).
- LRM: After HR v1 manual play (Day 11). This is why F8 sequences split first, HR v1 Day 11, HR v2 targets Day 18 — credit the plan for that sequencing, but the allocator ticket still pre-decides its UI.
- Replacement: Keep `EmploymentRegistry` API + "allocator is a client" seam as B. Target/priority/pin blank; backlog Q "what control scheme after manual?"

**F-035 — Node-snapping placement on a 2.5D `SitePlane` with `ModuleSockets.attachmentNodes`, corridors as straight node-to-node segments, max length, no intersections**
- Where: `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` §Placement; `DAY_BY_DAY_PLAN.md` "Locked on the spot"; `DECISION_LOG.md` #22
- Class: **B/C** — `SitePlane` abstraction is B (Phase 2 outposts — `EXPLORATION_AND_LONG_RANGE.md` §6). Node-snapping + straight segments as *the* mechanic before any ghost has been dragged is C.
- Missing: What placement feels intentional vs fiddly in zero-G with no ground? Banished has terrain; we have a rock (F4).
- LRM: Day 15 ghost draggable.
- Replacement: Keep `SitePlane` + `PlacementResult.Valid/Overlaps/OffPlane` + `Never hard-code y=0` as B seam. Node-snap vs free placement as Q with trigger "after you've placed 5 modules."

**F-036 — "Locked on the spot" framing of F4/F5/F6/F7 as solved**
- Where: `DAY_BY_DAY_PLAN.md` §Locked on the spot; `GLOSSARY.md` SitePlane; `ARCHITECTURE_CONSTITUTION.md` rules
- Class: Systemic **D pattern** — "Locked" language itself. See Category 7.

#### 6. Sequencing Lock-In — days coupled to upstream guesses (collapse map)

The plan helpfully lists 14 dependency flaws F1–F14 + resolves each. This section maps where a C/D guess still propagates collapse when vetoed.

| Guess (source) | Downstream consumers | Collapse radius if vetoed |
|---|---|---|
| **F-001/F-013 death thresholds + curves** (P0-D Day 20) | Day 23 report hours-until-empty, alert Food <12h, Day 26 playtest balance, Day 21 housing restfulness, `Kill` unwind (F-017) | Report columns + alerts + game-over tuning re-blank |
| **F-016 ScenarioDefinition fields** (Day 22) | Day 23 colony report, Day 25 session frame, Day 22 bootstrap deletes hand-authored Base | Bootstrap + session + report schemas collapse |
| **F-019 ledger schemas** (Day 9) | Day 10 facility/ship panels, Day 11 colonist panel (5 sentences), Day 23 Top(20)+sentences | All panels' "24h in/out/net, blocked time, duty sentences" columns collapse |
| **F-021/F-022 ports + voyage phases** (Days 5–6) | Day 13 ports/holding views, Day 12 ship interpolation, Day 10 ship panel queue position, `PopulationManager.Kill` ship release step | Presentation + ship panel + teardown collapse |
| **F-023/031 BuildingDefinition entries + Regolith** (Day 14) | Day 15 ghost (knows prefab), Day 16 site (cost=Regolith), Day 17 corridor/eva link, Day 22 scenario modules[], Day 24 deposit scatter | Entire build path collapses to "what material?" |
| **F-005/035 placement rules** (Day 15) | Day 16 site EVA link range, Day 17 corridor creation, Day 22 scenario corridors[] | Corridor build + EVA + bootstrap corridors collapse |
| **F-018 allocator priority/hysteresis** (Day 18) | HR v2 columns, Day 19 economy tuning, Day 23 duty failures | HR + tuning + report collapse |
| **F-025 HR layout + F-029 selection kinds** (Days 7–11) | Day 18 HR v2 target/pin, Day 23 report → select subject | HR + report UX collapse but seam (Assign/Unassign) survives |
| **F-010 clock 1/60** (Day 1) | Every "observable at speed" line, every per-hour rate, flight substep count, traversalHours wall-clock feel | Tuning of every rate (mitigated: rule "all rates per game-hour so nothing else changes" is correct seam — collapse is *feel* not *math*) |

Depth of lock-in: The plan fronts seams correctly (split G: Days 3–4, ModuleSockets Day 2, additive scenes Day 1), so mechanical collapse is limited. Content/spec collapse is deep: **Day 19 (72h integration) and Day 26 (playtest) consume 10+ guesses each** — if any C/D survives to those days, the playtest will be testing the guesses, not the game.

#### 7. Authoritative Framing — "Locked" designs for unbuilt systems

**F-037 — `ARCHITECTURE_CONSTITUTION.md` "The 12 binding rules"**
- Where: `ARCHITECTURE_CONSTITUTION.md` preamble + rules 1–12; `PACKET_INDEX.md` "if they disagree, this file wins"
- Class: **Systemic C/D framing** — Rules 1–5, 7, 9, 11 are A/B (direction, read-only views, narrow commands, one authority, tick-only, registries, explicit state, serialized fields) and should stay locked. Rules 6 (`IFacilityPerformanceProvider` channels), 10 (400-line ceiling), 8 (new facility = composition+content) pre-decide solutions for systems (power/morale/hazard, maintenance) that have never been built.
- Missing: Whether rule 6's channel model fits the third influence (it has fit staffing — but power/maintenance are unbuilt).
- LRM: When second channel is attempted (Phase 1 Power).
- Replacement: Keep rules 1–5, 7, 9, 11 as locked. Demote 6, 8, 10 to **"proposed — trigger pending"** with trigger: "revisit when second provider ships." Rule 12 (observable behavior + tests as design artifacts) is A per owner intent; keep.

**F-038 — `01_LOCKED_DESIGN.md` files as binding specifications**
- Where: `tickets/P0-A_Foundation/01_LOCKED_DESIGN.md`, `tickets/P0-S_Shuttle_Flight/01_LOCKED_DESIGN.md`, `tickets/P0-B_Interaction_UI/01_LOCKED_DESIGN.md`
- Class: **B/C mix** — P0-A's split is B (forced). P0-S's phase machine + port capture, and P0-B's ledger sentence template, are C/D locked against unplayed systems.
- Missing: See F-021/F-022/F-019
- LRM: Per ticket; extend Fable's instinct (P0-C/P0-D already DRAFT lock-after) to **demote P0-S §1–3 and P0-B §4/7 to proposal status** until their consumers play.
- Replacement: Keep file existence as seam; add banner "Proposed — trigger pending: [condition]" per section.

**F-039 — `GAME_DESIGN_DECISIONS.md` "Locked" vs "Open"**
- Where: `GAME_DESIGN_DECISIONS.md` §Locked
- Class: **C/D framing** — Genre, "ships are connective tissue," "corridors vs shuttles is strategic core" are A/B (owner intent, seam). Every value under People (fatigue constants, 24h day 3×8h, 1h/60s clock), Flight (6DOF no collisions), Placement (SitePlane straight corridors), Construction material (Regolith), Starting scenario (8/3-2-3/48h) are C/D under lock.
- Missing: Same per finding above.
- LRM: Per value; move to Open/Backlog with trigger.

**F-040 — `DECISION_LOG.md` 33 decisions recorded as decided, with 4 vetoes noted but still presented as log**
- Where: `DECISION_LOG.md` #1–33 + "Vetoes requested"
- Class: **C/D framing** — log is correct as trail; presentation as "decisions" (not "proposals") lets agents treat them as locked. Owner banner is present but weak.
- Replacement: Keep log as *proposals* with column "Status: proposed / trigger pending / vetoed / ratified." Vetoes #13, #14, #22, #27 stay; add vetoes for #23, #24, #27 composition, #33.

#### 8. Deletion of the Blank — spaces that should have stayed blank but were painted

**F-041 — `HOW_IT_WORKS.md` day-in-the-life narrative at minute-level precision (00:00 shift, 00:05 farm wakes at 0.65, 01:00 water low "keep above 20 refill to 60," 01:10 shuttle fly with holding beacon "Holding · #1," corridor walk arrival, 08:00 shift change, all-day eating, build Habitat)**
- Where: `HOW_IT_WORKS.md` §2 "A day in the colony"
- Class: **D** — narrates a game never played in enough detail it will anchor expectations; invented constants smuggled as story (0.65, 20/60, "Holding · #1", restfulness) become load-bearing mental model.
- Missing: How the day *actually* feels at watchable clock with walking + voyages + reports.
- LRM: After Day 6 corridor+ship co-exist and Day 9 ledgers answer "what *did* happen?"
- Replacement: Keep §1 heartbeat, §3 ownership, §4 arrows, §5 "what I changed" (with demotions). Replace §2 narrative with **seam-level walkthrough**: "A shift starts → reconciler assigns duty → resolver chooses Walk/Ship/Unreachable → walk transits or contract created → tick priorities 100→400 → history recorded → presentation interpolates." No numbers.

**F-042 — `GAPS_AND_OPEN_QUESTIONS.md` answering questions that should stay open**
- Where: `GAPS_AND_OPEN_QUESTIONS.md` §Open questions table with Recommendations: "Cutaway roofs on selected/hovered + corridor tubes with windows" • "Generator kit-bash first; buy later" • "Surplus food *and* free bed" • "Names + colour + one trait line in the slice"
- Class: **D** — each recommendation fills a blank the owner wanted open (interior visibility, module kit source, immigration gating, identity depth) with a guess that will be quoted as decision.
- Missing: Whether the player needs to *see* inside at all (GAPS gap: interior visibility drives art kit + presenter camera).
- LRM: Per question: interior visibility before P0-D (per GAPS: Day 12); module kit Day 13; resource chains Phase 1; immigration Phase 1.
- Replacement: Keep question ownership (Phase, why it matters). Move recommendations to **backlog hypotheses** with trigger: "decide when first farm worker sleeps in a bed and player asks 'where is he?'"

**F-043 — `ENVIRONMENT_ASSETS.md` and `EXPLORATION_AND_LONG_RANGE.md` § detailed art bible / field-vs-knowledge / scan view / conduit design**
- Where: `ENVIRONMENT_ASSETS.md` Art bible (faceting, palette hex, dust albedo, LOD tris, tumble 0.5–3°/s, volumetric Fog API) + spec E01–E04; `EXPLORATION_AND_LONG_RANGE.md` §1–6 (ResourceField 20 km / 300 m cells / 262k, SurveyKnowledge confidence, Sensor Mast, volumetric Texture3D cloud, conduit as "build once flow forever," mobile habitat, prefab towing, multiple site planes, probe `operatingRole == null`)
- Class: **Systemic D** — rich design for Phase 1–2 dropped into Phase 0 packets as seams Phase 0 "must keep open." Reads as spec, will be quoted as spec.
- Missing: Whether exploration is even the mid-game (depends on ice depleting day 5–8 at real consumption — unplayed).
- LRM: After first playable is played and water pressure is felt.
- Replacement: Keep §8 "Seams Phase 0 must keep open" as the only normative content — and demote each seam to **one-line hooks** (already well written: registry-by-range, SitePlane origin+normal, Loiter any point, vehicle-agnostic supply, null-role reserved, scatter reads field, serialize byte arrays). Move §1–6 to backlog as "nods, not spec" with banner upgrade.

---

## Dependency Collapse Map — Visual

```
Clock 1/60 (F-010) ─┬─► every observable + every rate + traversalHours feel
                    └─► flight substep + walk time + ledger buckets

TransitLink 0.25 (F-009) + Placement 15°/nodes (F-005/F-035)
                    ─► Pedestrian commit (F-017) ─► Kill unwind ─► Report

Regolith + BuildingDefinition list (F-023/F-031)
                    ─► Placement ghost (F-035) ─► Site (priority 8) ─► EVA link
                    ─► Scenario modules[]/corridors[] (F-016) ─► Bootstrap Day 22
                    ─► Collection pick by demand (F-006 → Day 14)

Ports capture/queue (F-021) + Voyage phases (F-022) + profile numbers (F-011)
                    ─► Holding view + ship panel queue ─► Kill ship release
                    ─► Day 19 72h run queue deadlock

Ledger buckets/sentences/Top20 (F-019) + Alert thresholds (F-006) + HUD (F-027)
                    ─► Facility/Ship/Colonist panels ─► Colony Report Day 23
                    ─► Balance tuning Day 19/26 (tests the guesses, not the game)

Death 24/72 + curves (F-001/F-013) + rest 0.5 (F-002) + fatigue latch (F-003)
                    ─► Housing auto-home (F-020) ─► Game over screen Day 25
                    ─► Playtest Day 26 narrative "we starved"

HR layout + selection kinds (F-025/F-029) + allocator 90/4h (F-018)
                    ─► Report duty failures ─► Tuning
```

Rule: When an upstream C/D is blanked, **everything downstream that consumed its fields becomes blank too** until re-derived from play.

---

## Rewritten Plan Skeleton — Replacement for `DAY_BY_DAY_PLAN.md`

Thumbtack the owner's liked structure: **commit each day's work one day ahead**, each day ends with **"Press Play and see X"**, each day states **what it expects to learn**, each day names **which decision points it feeds**. Only ~3–5 days are ever at working depth; everything beyond exists as a *question + trigger condition*, not as a design. The horizon may still sketch shapes; the depth may not.

### How to read it

- **Committed (depth = ticket):** Work + files + observable + learning question + decision fed.
- **Planned (depth = epic):** Named epics with owner + seam.
- **Backlog (depth = question):** Trigger-gated questions from Decision Backlog — never designed here.

Additive scenes + asmdefs + ModuleSockets + clock horizon are the only head starts Fable correctly sequenced (F1–F3, F11). Keep them.

---

### Committed Window — Next 3–5 Days (working depth)

The exact Day numbers roll; "Day N" means "next working day." Update this table at end of each day. Example starting point if plan were accepted today:

#### Day 1 — Skeleton and scene split  [P0-0 K01–K03]

- **Work:** Create `ColonyPrototype.Presentation` + `ColonyPrototype.UI` asmdefs (empty; Runtime unchanged). Split `SpaceSim.unity` into additive `Managers`/`Base`/`Environment`/`UI` + `Bootstrap` loader. Retune `SimulationManager.gameHoursPerRealSecond` to *hypothesis* `1/60` (flagged "propose, revisit Day 4/6").
- **May modify:** `EditorBuildSettings`, `ProjectVersion` not, scenes only. Forbidden: Runtime refs to new asmdefs.
- **Observable:** Press Play from `Bootstrap` → identical colony over 24 game-hours; `CurrentGameHour` advances ~1.0 per real minute at 1×; 10× completes a day in ~2.4 real minutes; no console errors.
- **Expect to learn:** Does additive load + singletons via `Instance` survive domain reload? Does `FindObjectsByType` in 5 startup paths still find colonists/ships across scenes? Is 1/60 *watchable* or just *slow*? What speed does a 10× run actually need?
- **Feeds decisions:** Clock revisit (F-010); scene ownership per packet (protect-list); `ModuleSockets` authoring (F-035).
- **Constitution rules touched:** 1, 7, 5, 11.

#### Day 2 — Module prefab convention

- **Work:** Runtime `ModuleSockets` data-only component + Editor gizmos. Convert CommandPod/Farm/Water Processor/Shuttle/Mining Ship to prefabs with `meshRoot` separation and placeholder sockets (no capture numbers; `dockPorts`/`approaches`/`holdingSlots` authored as transforms only, components come later). `Base.unity` becomes prefab instances.
- **Observable:** Same behavior; selecting a prefab instance shows validated sockets; SceneView draws nodes; prefab change propagates.
- **Expect to learn:** Which sockets are actually needed (workstations vs beds count?) Is meshRoot separation sufficient for ship interpolation later? Where do approach axes want to be?
- **Feeds decisions:** Placement seam `SitePlane` vs hard-coded y=0 (F-035); port geometry (F-021).

#### Day 3 — Split staffing part 1 ∥ ports seam ∥ flight seam (parallel, disjoint dirs)

- **Agents:** A: P0-A T00→T02 (`ScheduleReportFormatter`, `EmploymentRegistry`). B: P0-S S00–S01 `DockingPortComponent` + `DockingControlComponent` *seam only* (`RequestBerth` → `Granted|Queued|Denied` + `HoldingSlotFor`; no capture numbers). C: P0-S S02 `ShipFlightProfile` + `FlightIntegrator`/`FlightGuidance` *math only, with placeholder numbers, report hop times*.
- **Observable:** Identical behavior (no integration yet). Read S02 report: 3 thrust hypotheses → hop times for 500/1000/2000 m; no constants locked.
- **Expect to learn:** Does the split preserve `Assign` validation order and `SetDutyState` single-writer property (grep)? Which flight numbers produce 60–120 game-seconds without overshoot at 10×? What does the queue policy (priority vs FIFO) need to express?
- **Feeds decisions:** Allocator hysteresis (F-018); ledger schemas (still blank); voyage phases (F-022).

*At end of Day 3, re-plan Days 4–6 based on reports (flight hop times, split line counts).*

#### Day 4 — Split staffing part 2 ∥ voyage authority (seam)

- **Work:** P0-A T03–T04 (`CommuteBatcher` + reconcilers, `StaffingManager` ≤250 lines). P0-S S03 `ShipVoyageComponent` phase machine **minimal**: `Docked → Undocking → Cruise → RequestingBerth → Holding → Approach → Docked` + `Loiter`. No capture constants locked; guidance evaluated per substep, substeps flagged as spike.
- **Observable:** In a scratch scene, a context-menu voyage pushes back, cruises, holds if busy, docks; staffing behaves identically (capture Pilot 3 duty sequence before/after).
- **Expect to learn:** Does `TryClaimMovement` lease handover survive abort? Does reconstruction after domain reload readopt `Occupied` ports? Is enum list sufficient or missing a state?
- **Feeds decisions:** Whether `TryAbortToNearestBerth` is needed (F-022); port view needs.

#### Day 5 — Walkable base primitives ∥ ships fly for real

- **Work:** P0-A T05–T06 (`TransitLink`, `TransitGraph.Registry`, `RouteResolver` pure function, `PedestrianTransitComponent` at priority 400). P0-S S04 migrate executor/extraction/crew to voyages, timed load/unload *seam* (loading takes game time — rates blank), ports+holdingSlots on prefabs via `ModuleSockets.dockPorts`.
- **Observable:** Freight/passenger contracts complete via real voyages; mining loiters; loading takes visible time; ped transit `LegProgress01` climbs; no NRE when link disabled mid-leg → `Blocked`.
- **Expect to learn:** Is walk time 0.25h readable? Does Dijkstra over few links need optimization? Do we actually need `TransitLinkDefinition` SO or are scene components enough until construction builds them?
- **Feeds decisions:** TraversalHours (F-009) and capture (F-021) tuning; construction EVA seam (F-024).

*Rolling horizon rule: At end of each day, the next uncommitted day is specified **tomorrow** to working depth. Never specify beyond Day+3 at ticket depth.*

### Planned Epics — Depth = Epic, Not Ticket (no constants)

These exist as named epics with seam + owner, scheduled only when trigger fires.

**Epic H3 (corridors change behavior):** P0-A T07 — resolver in commutes, one corridor CommandPod–Farm, Water shuttle-served. Trigger: transit primitives exist (Day 5). *Does not* decide traversalHours or max corridor length.

**Epic A (Interaction foundation):** P0-B U00 camera + U01 shell — orbit/pan/zoom via Input System, `SelectionModel` + `SelectableMarker` (Facility/Colonist/Ship/Deposit only to start), HUD time + pause/speed + `ReadinessHistory.OnRecorded` event seam. Trigger: Ports matter (Day 4). *Does not* decide hotkeys, full HUD fields, or toasts allow-list.

**Epic R (Reporting read models):** P0-B U02 — `InventoryComponent.OnChanged` already exists; `OnRecorded` seam added. *Question:* what aggregation answers "why did Farm stop?" Trigger: first player ask after U00/U01 play. *Spike:* subscribe, log buckets for 24h, show Inspector.

**Epic I (Panels):** P0-B U03–U05 — Facility/Ship/Colonist/Deposit/Port panels as read-only projections over existing components + one ledger experiment. Trigger: reporting spike shows a question worth a panel. Columns not locked.

**Epic HR-Manual (P0-B U06 v1):** Assign/Unassign via `EmploymentRegistry`, Show `AssignmentResult` reason. Trigger: panels exist. *No* targets/priority/pin.

**Epic Facilities Presentation (P0-P):** `ColonistView` walk/hide, `FacilityPresenter` slots from `ModuleSockets.workstations/beds` + `ActiveWorkersChanged` seam. Trigger: colonists walk (Day 5). Slickness tuning after ships fly.

**Epic Ports Presentation (P0-S S05 slice):** Tick-interpolated `ShipView` on child meshRoot, `ThrusterSet` reads `commanded*`, port lights/clamps. Trigger: voyage authority compiles. Exact colors/beacon text not locked.

**Epic Build (P0-C):** `BuildingDefinition` type + `TryPlace → PlacementResult` seam, site as anchor+inventory( cost) + policy + staffing, `Demolish`, corridor as permanent `TransitLink`. Trigger: Day 6 corridors observable + P0-S ports validated. *Content entries, material identity, costs, rotation snap, EVA mechanism* all blank → backlog questions.

**Epic Staffing Targets + Allocator (P0-C allocator):** `StaffingTarget` list + `SetTarget` command seam. Allocator as client of `EmploymentRegistry`. Trigger: after HR-Manual has been used for two shifts. *Priority scale, hysteresis, badge, throttling* blank.

**Epic Live/Die (P0-D):** Needs bars seam, `Kill` seam, housing seam, ScenarioDefinition seam. Triggers per sub-epic (see Backlog). *Constants* blank.

Blanks deliberately not in these 26 days at ticket depth: save/load, power, research, morale, health, events, immigration, second site, final art, audio, tutorial — as before.

### The horizon may still sketch shapes

```text
G (split) ──► C (targets+allocator)   — seam after HR-Manual
A (camera/selection/shell) ──► I (panels) ──► HR-Manual ──► Report
H (transit) ──► Facilities view
S (voyages)   — independent, owns Vehicles/
D (needs/death/housing/scenario) — after H+S+HR
```

Arrows mean "must exist," not "is designed." Sequence is owner-committed one day ahead, not script-committed 26 days ahead.

### Daily rhythm (strengthened)

Morning: dispatch 1–3 agents with README + locked design (only committed day's ticket) + constitution seam. Midday: compile → press Play → check observable → **write what was learned** (one paragraph: what surprised, what the trigger answer now is). Afternoon: review diffs, commit per ticket with ID, update this plan's rolling window + backlog triggers ticked. If observable fails, next day starts with fix — never with new scope.

---

## Decision Backlog — Every C/D Extracted Into One Visible Register

Questions, triggers, who decides, current blank. "Locked" sections of `GAME_DESIGN_DECISIONS.md` and Constitution rules 6/8/10 and `DECISION_LOG.md` #13–14/22–24/27/33 are demoted to **Proposed — Trigger Pending** and point here.

| # | Question (blank that should exist) | Trigger that fires it — "decide when…" | Who decides | Current blank / hypothesis to discard |
|---|---|---|---|---|
| Q-01 | How lethal is starvation? (thresholds F-001, curves F-013) | You've watched a real 72h starvation run with freight still flying at new clock (post-Day 19) | Human | Thresholds blank; Kill seam stays |
| Q-02 | How punitive is homelessness? (0.5 F-002) | You've built first Habitat and watched 2 homeless sleep | Human | Multiplier blank; capacity=beds seam stays |
| Q-03 | How fast does fatigue accumulate/recover? (F-003) | You've watched 2 shifts of walking vs flying commutes | Human | Rates/latch blank |
| Q-04 | How much does one tech produce? (0.65 F-004) | First Farm sustains 8 colonists for 24h | Human | Curve hypothesis only |
| Q-05 | What snapping feels right? (15° F-005) | You've dragged a ghost 5 times | Human | Snap blank; PlacementResult seam stays |
| Q-06 | Which alerts deserve spam? (F-006) | You've been blocked/queued/starved once | Human | Thresholds blank; ledger seam stays |
| Q-07 | How much starting food is fair? (48h F-007) | 72h tuning run Day 19 | Human | Amount blank; startingStock seam stays |
| Q-08 | What crew mix is scarce-but-playable? (3/2/3 F-008) | After HR-Manual + allocator spike | Human | Split blank |
| Q-09 | How long is a corridor walk? (0.25h F-009) | First walk vs first flight side-by-side | Human | traversalHours hypothesis; link seam stays |
| Q-10 | What clock is watchable? (F-010) | Day 4 substepped flight + Day 6 queues at 10× | Human | 1/60 hypothesis; seam stays |
| Q-11 | What thrust makes a 2 km hop 60–120s? (F-011) | S02 report with 3 thrust hypotheses | Human + S02 spike | Numbers hypothesis; profile seam stays |
| Q-12 | How long should loading take? (F-012) | First contract completes via voyage | Human | Rates blank; TransferProgress01 seam stays |
| Q-13 | How to lay out thresholds on SitePlane? (F-035) | First 5 placements | Human | Straight/corridor rules hypothesis; SitePlane seam stays |
| Q-14 | What does ScenarioDefinition need? (F-016) | After corridor + port + site creation paths exist (Day 17) | Human | Field list blank; type+bootstrap-through-completion seam stays |
| Q-15 | What Kill order is safe? (F-017) | You've killed a colonist mid-walk and mid-flight (two spikes) | Human | Steps hypothesis; Kill seam stays |
| Q-16 | Does allocator need hysteresis/priority 90/badge? (F-018) | After HR-Manual for 2 shifts | Human | All allocator constants blank |
| Q-17 | What ledger answers "why did Farm stop?" (F-019) | First time you ask that after panels | Human + U02 spike | Buckets/sentences/Top20 blank; OnChanged/OnRecorded seams stay |
| Q-18 | Auto-home homeless? (F-020) | First Habitat, two homeless, HR shows count | Human | Auto-homing blank; SetHome seam stays |
| Q-19 | What capture geometry feels right? (F-021) | First berth with lights/clamps at new clock | Human | Capture numbers blank; RequestBerth seam stays |
| Q-20 | Which voyage phases deserve UI? (F-022) | S03 compiles + caller migration attempted | Human | Enum beyond 5 blank; authority seam stays |
| Q-21 | Which buildings + material in slice? (F-023/F-031) | After first ghost/site | Human | Entry list + Regolith hypothesis; BuildingDefinition/cost seam stays |
| Q-22 | What panel shows what? (HR/Report/HUD F-025/F-026/F-027) | After one 48h run explained from Inspector alone | Human | Layout/columns blank; command seam stays |
| Q-23 | What hotkeys? (F-028) | After camera + time controls exist | Human | Bindings blank; Input actions seam stays |
| Q-24 | Which selection kinds? (F-029) | When site/port/deposit first needs clicking | Human | Kinds beyond 4 blank |
| Q-25 | How sickly should hungry colonists look/work? (F-013) | Bars visible Day 20 | Human | Lerp blank |
| Q-26 | Priorities 1–10 vs 8 for site import vs food? (F-015) | First competing demands (site+farm) | Human | Priority values blank |
| Q-27 | Should Water Processor be automated? (F-033) | First Water shift staffed vs automated A/B spike | Human | Automated hypothesis; null-role seam stays |
| Q-28 | Targets+priority+pin control shape? (F-034) | After manual HR for 2 shifts | Human | Shape blank; EmploymentRegistry seam stays |
| Q-29 | EVA link vs EVA-port vs shuttle-to-nearest? (F-024) | First off-corridor site placed | Human | Mechanism blank; SitePlane seam stays |
| Q-30 | What does dust/asteroid look like? (F-043 palette/faceting/tumble) | After E01 scratch scene | Human + art | Bible as hypothesis; generator tool seam stays |
| Q-31 | Is ice depleting day 5–8 right? (F-043) | After 5-day run | Human | Timing hypothesis |
| Q-32 | Interior visibility — see inside? (GAPS) | When you watch a worker at a station and ask "where is he?" | Human | Blank; trigger Day 12 |

*Every Q sits in `DECISION_BACKLOG.md` (propose to create next). Demote "locked" sections: `GAME_DESIGN_DECISIONS.md` §People/Flight/Placement/Construction/Scenario volatile lines + Constitution rules 6/8/10 + `DECISION_LOG.md` #13–14/22–24/27/33 → "Proposed — see Backlog Q-xx, trigger pending."*

---

## De-Specification Pass — Worst 5 Tickets Rewritten as Seams + First Questions

Worst offenders: **P0-D Day 20 Needs/Death/Housing**, **Day 22 ScenarioDefinition**, **Day 23 Report**, **Day 15 Placement**, **Day 18 Allocator**. Each rewritten to specify only seams and first-questions; guesswork moved to backlog.

### D-01 Rewritten: Day 20 — Needs and Death (was `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` §Needs+Death)

**Before (over-specified):** ColonistNeedsComponent 0..1, needDrainPerHour authored, recovery `lerp(0.4,1.0,min(nutrition,hydration))`, work multiplier same curve, thresholds hydration 0 24h / nutrition 0 72h → Kill, 8-step unwind with names per system.

**After — Seam + Spike:**

> Must create: `People/Needs/ColonistNeedsComponent.cs` `[SerializeField] float nutrition, hydration` accessors + `PopulationResourceConsumer` reporting `Fed` per tick; `ColonistStatusComponent.WorkEffectiveness` scalar read by `StaffingComponent` (counts stay integer).
>
> Must create: `PopulationManager.Kill(colonist, reason) → KillResult` with contract "unwind in dependency order (duty → employment → passenger/tansit → ship lease → history → unregister → destroy), log and continue on any step failure, return what could not be unwound." No step list locked.
>
> May modify: `ColonistReconciler` sleep recovery reads needs; `StaffingComponent` folds effectiveness into multiplier.
>
> Forbidden: Any threshold, curve, or rate constant. Any sentence like "hydration 0 for N hours." `Needs` values are logged, not acted upon beyond bars and multipliers, until Backlog Q-01 spike.
>
> Observable: Disable Farm → bars fall over hours visible in Inspector; `WorkEffectiveness` declines; colonist does **not** die yet — death is behind a `DEATH_SPIKE_ENABLED` guard and manual `Kill` command from debug menu. Spike: run 72h invulnerable, log curves, choose thresholds after.
>
> Feeds: Q-01, Q-13, Q-25.

### D-02 Rewritten: Day 21 — Housing (was §Housing)

**Before:** HabitationComponent capacity = beds.Count, `PopulationManager.SetHome`, homeless restfulness 0.5, Habitat auto-homes homeless allocator-style.

**After:**

> Must create: `HabitationComponent` with `[SerializeField] List<Transform> beds` authority for capacity; `PopulationManager.SetHome(colonist, anchor) → HomeResult` (rejects full housing). Homeless flag visible in HR.
>
> Forbidden: `0.5` restfulness constant; any auto-homing. Beds show `restfulnessMultiplier` as **authored per housing prefab** (content seam), blank until Habitat built. Auto-homing is Backlog Q-18 with trigger "first Habitat completed with two homeless."
>
> Observable: 8 colonists / 6 beds → two show `Homeless` + low restfulness (authored guess); build Habitat → beds increase, flag clears only on explicit `SetHome` (manual for now).
>
> Feeds: Q-02, Q-18.

### D-03 Rewritten: Day 15 — Placement (was `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` §Placement + DAY_BY_DAY_PLAN "Locked on the spot")

**Before:** SitePlane origin+normal planeId=1, PlacementRules.Evaluate with Valid/Overlaps/OffPlane/CorridorTooLong/CorridorNotStraight/CorridorIntersects/NoNodeInReach/Locked, free XZ + 15° snap + node snap, corridor straight node-to-node ≤ max, PlacementGhost tinted, BuildMenu with cost-holding indicator.

**After:**

> Must create: `Construction/SitePlane {origin, normal, planeId}` (starting base is plane #1); `PlacementRules.Evaluate(def, pose, plane) → PlacementResult {Valid, Overlaps, OffPlane}` — *only*. `ConstructionManager.TryPlace` creates a placeholder site on Valid.
>
> Must create (Presentation): `PlacementGhost` mesh from `ModuleSockets.meshRoot` tinted valid/invalid; snap preview **when nodes near** but granularity blank.
>
> May modify: none beyond scenes.
>
> Forbidden: `CorridorTooLong/CorridorNotStraight/CorridorIntersects/NoNodeInReach/Locked`, `maxCorridorLength`, `15°`, `evaRange`, `BuildingDefinition.isCorridor`, BuildMenu cost-holding indicator (informational affinity before freight). All are Backlog Q-05/13/21.
>
> Observable: Pick Habitat, drag ghost, see valid/invalid coloring and node snap when near; confirm creates empty site (no demand yet). Spike: place 5 modules free vs snapped, record which felt intentional.
>
> Feeds: Q-05, Q-13, Q-21, Q-35.

### D-04 Rewritten: Day 22 — Scenario Bootstrap (was §Scenario + F13)

**Before:** ScenarioDefinition full field list (sitePlanes[], modules[] {pose, planeId, initialStock[], staffingTargets[]}, corridors[], ships[], deposits[], colonists[], startHour, clockDefaults), ScenarioBootstrap through construction completion paths, delete hand-authored Base.

**After:**

> Must create: `ScenarioDefinition : ScriptableObject` *empty-marker type* + `ScenarioBootstrap` that **calls whatever creation path construction actually provides** to spawn modules/corridors/ships/deposits/colonists. The asset's fields are **not specified here** — they are the first question the spike answers.
>
> Spike ticket first: List the Day 16/17 creation calls bootstrap must call; derive the *minimal* scenario fields from that list (likely `modules {BuildingDefinition, pose, planeId}` + `corridors {node pair}` + `ships {prefab, dock}` + `deposits {prefab, pos}` + `colonists {classes, home}` — but do not lock initialStock, staffingTargets, startHour, sitePlanes[] until needed). Add fields one at a time as bootstrap fails without them.
>
> Forbidden: Locking staffingTargets[], amount, startHour, clockDefaults in schema.
>
> Observable: New Game builds starting colony from data via same path as player-built corridor; changing the (minimal) asset changes the start without editing `Base.unity`.
>
> Feeds: Q-14.

### D-05 Rewritten: Day 23 + Day 18 — Colony Report & Allocator (were P0-B §7 + P0-C §Staffing targets)

**Before (Report):** Population/resources/blocked Top(20)/duty failures/transport sections + thresholds food 12h etc. (F-006/F-019/F-026). **Before (Allocator):** StaffingTarget per role×shift, workPriority 1–10, WorkforceAllocator tick 90 hysteresis 4h fill by priority via Assign.

**After — Two linked, de-specified:**

> **Report — Seam first:** Keep only `ResourceFlowLedger`/`BlockedTimeLedger`/`DutyReportBuilder` as *questions*, not schemas. Day 9 U02 spike: subscribe `OnChanged`/`OnRecorded`, log 24h; Day 10 panels show **one** facility's `blockedReason` + **one** inventory's `onHand` with a manually computed "why" sentence. Full report columns blank; alert thresholds blank (Q-06/Q-17/Q-22). Day 23 trigger: "when you need to explain a starvation without opening Inspector."
>
> **Allocator — Seam first:** Keep `StaffingTarget {role, shift, target}` list + `SetTarget`/`SetPriority`/`SetPinned` commands as seam (content-authored default, player-editable). Allocator itself is **not built Day 18**. Day 18 becomes `HR-Manual spike`: use HR v1 manually for two shifts; log thrash/pain; allocator ticket fires only if trigger Q-16/Q-28 says manual is painful. If built, it is "client of EmploymentRegistry, hysteresis blank, priority blank, throttle blank."
>
> Observable (Report spike): After 24h, a one-line sentence "Farm: no Water for 6h because Shuttle was Holding at Farm" is correct (joined from a ledger you just invented). Observable (Allocator de-spec): HR v1 manual survives; allocator blank.
>
> Feeds: Q-06, Q-16, Q-17, Q-22, Q-28.

*Note: Day 19 integration and Day 24 environment remain as before but now consume no constants — they become the spikes that *produce* the constants.*

---

## What to Delete / Demote From "Locked" Docs

- **`GAME_DESIGN_DECISIONS.md` §Locked → move to `DECISION_BACKLOG.md` Q-01–Q-15:** People constants, clock `1/60`, flight collisionless 6DOF as tuning, placement 15°/straight/corridor length, `Regolith`, `ConstructionRate` curve, staffing latch, housing 0.5, starting scenario composition. Keep genre, harvest→builder identity, corridor-vs-shuttle seam, hybrid intent (shape blank), corridor EVA never inventing third option. Owner's "automated Water Processor" stays as hypothesis with Q-27 trigger.
- **`ARCHITECTURE_CONSTITUTION.md` rules 6, 8, 10 → "Proposed — trigger pending"** (see F-037). Keep rules 1–5, 7, 9, 11–12 locked.
- **`01_LOCKED_DESIGN.md` files → add banner per section** "Proposed — trigger pending: [after first X is observed]." P0-A stays locked; P0-S §1–3 and P0-B §4/7 demoted.
- **`DECISION_LOG.md` 33 → add Status column** `Proposed / Trigger pending (Q-xx) / Ratified`; add veto requests for #23, #24, #27, #33 extent beyond #13/14/22/27.
- **`HOW_IT_WORKS.md` §2 → replace** with seam-level walkthrough (no numbers, no 00:00 fiction).
- **`GAPS_AND_OPEN_QUESTIONS.md` Recommendations → move** to backlog hypotheses with triggers.
- **`EXPLORATION_AND_LONG_RANGE.md` §1–6 → banner "nods, not spec"**; keep §8 one-line hooks as proposed seams.

---

## Definition of Done — Can a Reader Answer in One Minute?

After revised plan:

1. **What are we building in the next three days, and what will we learn?**
   > Days 1–3: additive scenes + ModuleSockets + staffing split seam + port/voyage seams with placeholder numbers. We expect to learn: does cross-scene singletons+registries survive, is 1/60 watchable, which thrust makes a hop 60–120s, did split preserve Assign order and single duty-writer. Next window re-planned tomorrow from those reports. **No constants locked.**

2. **What have we explicitly refused to decide yet, and what triggers each?**
   > 32 blanks in Decision Backlog (Q-01–Q-32) — death thresholds, restfulness, fatigue, farm curve, snapping, alerts, roster, walk time, clock, thrust, loading, placement, scenario fields, kill order, allocator hysteresis, ledger shape, auto-homing, capture geometry, phases, buildings, hotkeys, selection kinds, etc. Each has a trigger of form "decide when you've watched your first X." **Zero decisions about unplayed systems ship without that trigger.**

3. **Which decisions survive because existing code forces them?**
   > Inventory sole authority, converter-never-counts, explicit employment, pilot lease, publish-then-commit, extraction outside freight, `currentLocation` arrival commit, tick priorities, `StaffingManager` facade split shape, `ModuleSockets` convention, additive-scene seam, `RequestBerth`/`Voyage` authority as *mechanisms* (not numbers). All cited to file:line in FACTCHECK. **Class A stays.**

Plan contains **zero** decisions about unbuilt systems without a trigger condition attached. Spaces stay blank until we put the right thing there.

---

## Guardrails (kept)

- No new 26-day design produced — only a rolling 3–5-day window + trigger-gated backlog.
- No finding marked "wrong" — every finding marked **VETO-ABLE** with preserved reasoning trail.
- No blank filled with my guess — where seam unclear, finding states "here is the blank; here is how we will learn what belongs there."
- Real codebase facts respected (FACTCHECK VERIFIED TRUE).
- Plan remains runnable day-by-day: each committed day still ends with "Press Play and see." Blankness never excuses vagueness about *today*.

---

## Appendix — File Citations Used

- `PLANNING_REVIEW_FACTCHECK.md` (truth baseline, A5 discrepancy note)
- `HOW_IT_WORKS.md` §2 narrative, §3 ownership table, §5 changes/§6 non-changes
- `GAME_DESIGN_DECISIONS.md` Locked/Open, `ARCHITECTURE_CONSTITUTION.md` 12 rules, `DECISION_LOG.md` #1–33 + vetoes
- `DAY_BY_DAY_PLAN.md` Days 1–26, F1–F14, "Locked on the spot"
- `ROADMAP.md` epics G/H/A/I/B/C/D/E/S/F, `PACKET_INDEX.md` ownership, `GAPS_AND_OPEN_QUESTIONS.md` Recommendations, `ENVIRONMENT_ASSETS.md` art bible + E01–E04, `EXPLORATION_AND_LONG_RANGE.md` §1–8 seams, `GLOSSARY.md`/`START_HERE.md`
- Packets: `tickets/P0-0_Skeleton/00_README_AND_TICKETS.md` K01–K06, `tickets/P0-A_Foundation/00_README_FIRST.md` + `01_LOCKED_DESIGN.md` Parts 1–2 + T00–T08, `tickets/P0-S_Shuttle_Flight/00_README_FIRST.md` + `01_LOCKED_DESIGN.md` §0–6, `tickets/P0-B_Interaction_UI/00_README_FIRST.md` + `01_LOCKED_DESIGN.md` + `02_TICKETS.md` U00–U08, `tickets/P0-P_Presentation_People/00_README_AND_TICKETS.md` P01–P04, `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` (DRAFT), `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` (DRAFT), `tickets/P0-E_Environment/00_README_FIRST.md`, `tickets/P1-X_Exploration/00_STUB.md`

*End of Muse Spark prong. Await two sibling prongs for synthesis; do not treat this prong as the synthesis until compared.*

