# glm5.3 — Adversarial "Blank Space" Audit of Fable's Plan

Reviewer: Buffy (glm5.3), executing `PROMPT_ADVERSARIAL_PLAN_AUDIT.md`.
Plan under review: Fable's preliminary planning review (commit `75e5784`), banner-marked TENTATIVE.
Mechanical accuracy: already verified in `PLANNING_REVIEW_FACTCHECK.md` — every hard number matched. **Accuracy is not the question. Premature specification is.**

Owner's method (the standard every finding is judged against): *"I want to make intentional bespoke decisions, and discover things through development and testing, rather than make guesses ten steps down the road... I want a space to stay blank until we put the right thing there... build incrementally on top of what we learned the day before, not be locked into a guess about what system Q is when we only have systems A and B working."*

Worked example of the disease: P0-D specifies hydration-death at 24h and nutrition-death at 72h — for a needs system that will first exist on Day 20, with zero play sessions between writing and needing those numbers. Those are guesses dressed as facts. Contrast the fatigue rates (+0.10/h working, −0.10/h sleeping, latch at 0.90/0.20): those are **recorded facts from the existing codebase**, and Fable cites them as constraints — which is exactly the difference between a decision grounded in reality and one painted into a blank.

---

## 0. The one-sentence verdict

**The plan's structure is genuinely good and its facts are accurate; its failure mode is that it cannot resist answering questions nobody has earned the right to answer yet — the fix is not to redo the plan but to re-grade its 3,695 lines from "decided" to "triggered."**

---

## 1. Executive summary

Fable's plan is a *structure* worth keeping (daily cadence, daily observables, dependency-flaw table, Dijkstra-links-not-NavMesh, additive scenes, read models) wrapped around *content* that is a mix of (a) facts about the real codebase, (b) mechanical consequences of those facts, and (c) invented answers to questions that development itself would answer better. The invented answers cluster exactly where Fable had no code to verify against — needs/death (P0-D), placement (Day 15), scenario (Day 22), reporting schemas (Day 9), and every UI layout in P0-B.

The owner asked for one thing: don't let anyone paint "app-shaped bullshit" into a blank space. The plan paints at a very high level of craft. This audit extracts **43 findings across four classes**: 7 D-class deletions (§4), 18 C-class demotions to trigger-gated blanks (§5), 6 B-class keeps-with-triggers (§6), plus 12 A-class verifications (§3) that the plan is grounded where it claims to be. No finding calls anything "wrong" — every one is VETO-ABLE, preserves Fable's reasoning trail, and names the blank that should exist instead.

Key structural recommendation: keep Fable's daily "Press Play and see" cadence — the owner explicitly likes it — but add a per-day "what we expect to learn" field, cap working depth at a 3–5 day committed window, and move every C/D-class decision to a trigger-gated `DECISION_BACKLOG.md` so the blanks stay blank until the trigger fires.

---

## 2. Method and classification scheme

Per the audit prompt: every decision was extracted from every doc and ticket, classified:

- **A — Forced.** Mechanical consequence of existing code or the owner's stated intent. Keep.
- **B — Needed now.** Downstream work is genuinely blocked without it; no experiment could inform it first. Keep with a revisit trigger.
- **C — Decidable later.** Blank it; specify only the seam + question + experiment + last responsible moment.
- **D — App-shaped bullshit.** Invented because planning AIs fill blanks. Delete from tickets; record in the backlog as an open question.

The borderline between B and C used one test: *"could a 30-minute play session or a scratch scene inform this better than a guess?"* If yes → C.

---

## 3. What's genuinely forced by existing code (A-class — the plan's honest core)

These verifications are findings too: they mark where the plan is grounded and should survive the audit untouched.

- **A1.** StaffingManager split (P0-A Part 1) — 1,063 lines is a fact; the facade-preserving split is the minimal consequence. **Class A.**
- **A2.** `TransitLink/TransitGraph/RouteResolver` Dijkstra over authored links (P0-A Part 2). "No NavMesh, no grid" is a mechanical consequence of the owner's "no terrain" intent — keep the *principle* forced; the only free variables (traversalHours default, evaRange) are C-class (findings 1, 2).
- **A3.** Additive scene split (P0-0 K02) — forced by the verified fact that parallel agents currently collide on `SpaceSim.unity`. **Class A.** (K02's exact scene list is A-class; placing the camera in Managers.unity "for now" is presentation-only and A-consistent.)
- **A4.** Clock retune (P0-S §0): at 1 game-hour/real-second, a full 8h shift passes in 8 real seconds — no watchable day/shift rhythm, no visible commute, no readable approach corridor. Everything is already per-game-hour so retune is mechanically safe. **Class A.** (The *feel* of 1/60 vs 1/30 is a Day 1 knob to feel in the editor, not a locked number — finding C8 covers the knob's governance.)
- A4b. **Voyage phase machine shape** (Docked→Undocking→Cruise→RequestingBerth→Holding→Approach→FinalDocking) is largely **Class A**: every state is a named blocker source in existing blocked-diagnostics; Loitering already exists; the machine is the factored form of what three components hand-roll today (verified: `ShipMovementComponent` = 23 lines, exactly 3 callers). **Class A.**
- **A5.** One voyage authority replacing three hand-rolled movers — forced by the verified fact of three hand-rolled movers. **Class A.**
- **A6.** Read models (P0-B §4) forced by rule 18 ("UI numbers come only from read models") which itself is forced by rule 4 (one authority per fact), which is a fact of the existing architecture. **Class A** — the *existence* of ledgers is forced; their *schemas* (bucket widths, 24h rolling window) are C-class (finding C14).
- **A7.** `ReadinessHistory.OnRecorded` as the only Runtime seam for UI — forced by rule 2/18. **Class A.**
- **A8.** Construction site as pure composition (anchor + inventory + stock policy + staffing + performance) — forced by constitution rule 8 ("new facility is composition + content"). **Class A.** (Its numbers are C/D — findings C17, C20, C20b.)
- **A9.** Death as one explicit `Kill()` unwinding dependencies in order, instead of `Destroy(go)`. The *shape* is forced by the verified six-system coupling. **Class A.** (The death *timers* are D-class — finding D1; the sequence itself is part of the forced shape.)
- **A10.** `ModuleSockets` (P0-0 K04) — forced by the verified fact that ports, presenter, construction, and ship view all need the same sockets. **Class A.** (The bed/socket *counts* authored in K05 are C — finding C11.)
- **A11.** The 12 constitution rules. **Class A** — they are refactorings of rules already enforced by the existing code (assembler of facts, not inventions). The "binding" framing is governance, not code — finding B31.)
- **A12.** `P0-S S00` characterization-first ticket: zero source changes; map call sites before refactoring. This is the plan's best process instinct and the model for what more tickets should look like. **Class A.**

---

## 3b. One structural ambiguity Fable flagged correctly

The P0-C draft contains an **honest open question** — how do builders reach a shuttle-served site (temporary "EVA port" vs. drop at nearest module + EVA — with a recommendation), and "where does workPriority live — decide at lock." Fable marked them **open** instead of filling them. That is the correct behavior per the owner's method. The audit's findings are about everything else — the plan already contains the right *pattern* (open question + recommendation + decision-at-lock) and needs it applied uniformly.

---

## 4. D-class findings — app-shaped bullshit: delete from tickets, keep as open questions

These are invented answers to questions that development itself will answer better. Delete from the ticket text; the shape (not the number) survives as the open question.

1. **D1. Death timers: hydration 24h / nutrition 72h** (P0-D; GAME_DESIGN_DECISIONS "hard threshold → death"). Invented constants for a system that has never run. The plan even has the right answer in its own vocabulary: "balance numbers... Day 19 tuning is the first pass" — but you cannot tune a death timer that was locked 6 days before the first playable. **Action:** blank the numbers in the ticket; the ticket's job is the unwinding *mechanism* (Class A9). Keep the *question* "what makes death feel fair in playtests?" in the backlog. Trigger: first playtest after needs exist.
2. **D2. Homeless restfulness multiplier 0.5** (P0-D Day 21). A number for how badly it feels to have no bed, written before anyone has been homeless in the game. Same pattern as D1. **Action:** blank it; ticket builds the homeless state + HR display (Class A); the multiplier's value is a playtest question. Trigger: first session with a housing shortage.
3. **D3. Alert thresholds** — Food/Water `< 12h`, blocked > 2h, holding > 1h, site starved > 8h (P0-B §7, Day 23). Four invented constants that decide *when the game nags the player*. Nuisance vs. usefulness is 100% playtest territory. **Action:** blank thresholds; build the alert *plumbing* (rule-evaluation at 4 Hz, allow-list) — that part is A-class plumbing; thresholds become authored, trivially editable constants whose defaults are set after the first sessions. Trigger: first playtest of the report screen.
4. **D4. Watchable load times: 0.002 h/unit ≈ 7 game-s, 0.01 h/passenger** (P0-S §4). Invented "watchability" constants. What rate *feels* like loading rather than teleporting is exactly what Day 5's "Press Play" observable is for. **Action:** blank; the phase takes time (A-class), the rate is a Day 5 knob, set by watching. Trigger: Day 5 observable.
5. D5. **15° rotation snapping** (Day 15, P0-C, Decision Log #22). The owner has never placed a building. Whether snapping is necessary at all depends on whether free placement feels fiddly — which is only knowable with a mouse in hand. **Action:** keep `PlacementRules` (A-class), blank the degree. Trigger: first placement session (Day 15 playtest).
6. **D6. The exact `ScenarioDefinition` field list** (P0-D Day 22: sitePlanes[], modules[]{...pose, planeId, initialStock[], staffingTargets[]}, corridors[]{moduleA,nodeA,moduleB,nodeB}, ships[]{prefab, dockModule, portIndex, flightProfile}, deposits[]{...}, colonists[]{name, classes, skills, home, employment?}, startHour, clockDefaults). A full data schema for a bootstrap that Day 22 will consume from systems that change shape over the next 21 days. **Action:** the ticket becomes: "bootstrap spawns the running base from data instead of a scene, reusing the Day 14–21 creation paths; field list = whatever the walk-back from the running colony forces." The *principle* (bootstrap through construction completion path — Decision #28, A-class) is kept. Trigger: end of Day 21, when the set of things needing a starting value is *known*, not guessed.
7. **D7. Colony-report column lists and HR v2 layout** (P0-B §5–7, Day 23, Day 18). Full column inventories for panels nobody has looked at. (HR v1's structure — workplaces → role → shift rows with candidates — is closer to A-class since `GetAssignmentCandidates` exists; the *layouts* are the speculation.) **Action:** keep the data sources (A-class); blank the layouts; "panel shows what the player asks about first in the first unguided session" is the trigger.

**D-class summary:** the pattern in every case is identical — a *mechanism* the code needs (A), wrapped around a *number or layout* only play can teach (D). De-specify the number, keep the mechanism.

**Severity note:** D1–D4 are the exact "app-shaped bullshit" the owner described — numbers with the *form* of design and none of the substance, which will anchor expectations the moment an agent implements them without question.

---

## 5. C-class findings — decidable later: demote to trigger-gated blanks

These have a real decision in them, but development can inform it better than a guess. Specify the seam; leave the choice blank until the trigger.

8. **C8. Clock value 1/60 vs 1/30** (P0-S §0). Direction is A4 (retune); the value is a feel-knob. **Trigger:** Day 1 — watch a full day and a full commute at 1/30, 1/45, 1/60 in-editor. Decide from feel, not from the plan. (Decision Log #14 already lists the retune as a requested veto — good.)
9. **C9. Corridor traversalHours 0.25h** (P0-A locked design, T07 scene ticket). Invented walking time for a corridor whose length isn't even authored yet. **Trigger:** first walking commute at the new clock (Day 5–6 observable); decide when you can watch one.
10. **C10. `evaRange` (site→nearest module EVA link distance)** (P0-C draft). A radius that decides base sprawl feel, unknowable until bases sprawl. **Trigger:** first placed building (Day 16).
11. **C11. Socket counts on module prefabs** — CommandPod 2 ports/2 holding/8 beds/4 nodes, Farm 1 port/2 stations/2 nodes, WaterProcessor 1/0/0/2 (K05). Port counts are the "port count is a build decision" made before the player can build ports. **Trigger:** Day 5–6 when ports and queues are watchable, or Day 15 when placement exists; start with the current scene's needs (1 used port per station currently) and add only what a placed building demands.
12. **C11b. "48h of food" starting stock and 3/2/3 colonist split** (Decision #27, GAME_DESIGN_DECISIONS, HOW_IT_WORKS). Partially A (owner picked the roster split; 8 colonists serves the demonstration), partially D (48h is a balance number whose only defense is "it sounds right"). **Action:** keep roster (A), blank the 48h figure (D-pattern, backfilled at Day 19 tuning). Trigger: Day 19 economy pass.
12b. **C11b2. "Starting ice depletes day 5–8"** (Decision #32, EXPLORATION doc, K05/K06 context). A designed mid-game trigger whose *existence* is a real design intent (owner chose it), but whose **timing target is a balance claim that needs the actual consumption loop to exist** — consumption exists today, but the needs system that makes water matter doesn't. **Trigger:** Day 19–20 tuning pass; keep the intent, let the number come from measurement.
12c. **C12. Water Processor "automated"** — actually reclassify this one: the owner's stated rationale ("don't spend one of 8 colonists on it") is documented as user-choice adjacent (Decision #27 "incl. automated Water Processor" is listed in the vetoes-requested list), so it's a **B-class keep-with-trigger** — see finding 28.
13. **C13. Allocator policy numbers — priority range 1–10, hysteresis 4 game-hours, throttled once per game-hour** (P0-C). The allocator's *existence* is owner-chosen (Decision #6, A-class); the governor constants are unplayed. **Trigger:** first week of allocator-driven play (Day 18+).
14. **C14. Ledger schemas: 24h rolling window, hourly buckets, 4 Hz panel rebuild** (P0-B §4, U02). The ledger *existence* is A6; the window/bucket/rebuild constants are unplayed UI-performance guesses. **Trigger:** Day 9–10 observables; the window should be "long enough to answer the player's actual question," which is discoverable only by asking it.
15. **C15. Alert *evaluation* at 4 Hz and the 8-event toast list** (P0-B §3). Plumbing A, constants C. **Trigger:** Day 8 observable.
16. **C16. `BerthRequest` grant policy: priority desc → requestedHour asc → registration order** (P0-S §1). Reasonable; unplayed; trivially changeable later behind `RequestBerth`. **Trigger:** first queue pile-up (Day 5–6); the seam (validated command + result struct) already isolates it.
17. **C17. `Regolith` as the single construction material** (Decision #23). Defensible scope decision the owner should ratify; the *choice of which material* is blankable (any Discrete resource works). **Trigger:** Day 14 content authoring — and note the open question in GAME_DESIGN_DECISIONS already asks "which construction material(s) exist first?" — the plan answers in one place what it leaves open in another.
18. **C18. BuildingDefinition entry list** (Corridor/Habitat/Farm/WaterProcessor/DockPortModule) — **C-class:** the *first* buildable should be whatever Day 15–16 testing demands, not a pre-chosen menu. **Trigger:** Day 15.
19. **C19. Corridor cost curve: traversalHours from length** (P0-C Day 17 "traversalHours from length"). A formula for walk-feel, invented. **Trigger:** first corridor built by the construction system (Day 17).
20. **C20. Demolish refund "a fraction of cost"** (P0-C). "A fraction" is honest vagueness; the actual policy is unplayed. **Trigger:** first demolition (Day 17).
21. **C20b. Site import priority 8** (P0-C). One more invented priority constant competing against the stock-policy system that already exists with its own priorities. **Trigger:** Day 16, when sites actually compete with colony consumption for shuttle time.
23. **C22. Camera specifics: edge-pan, WASD, wheel zoom, focus-on-F, pivot on y=0, unscaled delta** (P0-B §1, U00). Camera *feel* is the most personal spec there is; the rig *shape* (pivot orbit/pan/zoom) is A-consistent with the owner's 3D colony. **Trigger:** Day 7 observable — "fly it and see if it feels like your game."
24. **C23. HUD contents: population, food/water + NetPerHour arrows, day/hour** (P0-B §3). Reading *data* is forced by the ledger architecture; *what the player wants at a glance* is a play question. **Trigger:** Day 8.
25. **C24. The word "locked" itself** (P0-A/P0-S `01_LOCKED_DESIGN.md`, "Locked on the spot" in DAY_BY_DAY_PLAN, GAME_DESIGN_DECISIONS "Locked" section, Constitution "binding"). Locking is the mechanism by which a guess becomes load-bearing. The *practice* (freeze a design so an agent can implement without reinterpretation) is a good process for A-class material; applied to C/D material it converts guesses into debt. **Action:** rename to "PROPOSED — trigger-gated"; keep lock discipline for A-class only. Trigger for re-locking anything: the human's sign-off, not the planner's confidence.

---

## 6. B-class findings — needed now, but with revisit triggers

These are genuinely load-bearing *now* — the plan can't run without them — but they deserve explicit revisit triggers because they were still chosen without play evidence.

26. **B26. Hybrid control model** (Decision #6, owner choice per Decision Log "User choice"). **Class B** — owner-stated intent is the owner's to make; the audit's job is only to note it's ratifiable-not-invented. Keep; trigger: revisit after HR v2 exists and a week of allocator play.
27. **B27. UI Toolkit over uGUI** (Decision #17, "lock to avoid mixing"). Owner-ratifiable preference; the *lock* is premature (the owner has never seen the game's UI). **Trigger:** Day 8 first screen — cheap to revisit before 6 panels exist, expensive after.
28. **B28. Automated Water Processor** (Decision #27) — already in the vetoes-requested list; keep B, trigger = the owner's veto answer.
29. **B29. Death by attrition / game-over at population zero** — owner-chosen genre identity (Decision #2 "User choice"), needs the mechanism now (A9). Keep.
30. **B30. "Both modes first-class; the choice is the strategic core"** (Decision #7, owner: "that's part of the game's strategy core"). Owner-stated, load-bearing for P0-A. Keep.
31. **B31. Constitution's "binding" framing + "paste into every ticket"** (Constitution header; ROADMAP §6). The rules are A11 (grounded); the *governance* (paste-verbatim, "this file wins") is process, not code-forced, and "binding" language converts proposals into authority without a ratification event. **Trigger:** owner's formal acceptance of the plan; until then the TENTATIVE banners already carry the governance question.

---

## 7. Dependency collapse map — when a guess dies, what falls?

The plan's C/D decisions are load-bearing for downstream days. Map:

```
D6 ScenarioDefinition schema (Day 22)  ← consumed by: nothing (terminal day)
C9 corridor traversalHours (Day 3–6)   ← consumed by: Day 6 corridor observable, Day 17 cost curve (C19)
C17 Regolith choice (Day 14)           ← consumed by: Day 14–16 site inventory/policy (C20b), Day 19 tuning (C11b)
C11 socket counts (Day 2)              ← consumed by: Day 13 presenter slots, Day 21 housing capacity
D3 alert thresholds (Day 23)           ← consumed by: nothing (terminal); trivially re-editable constants
D1/D2 death/rest constants (Day 20–21) ← consumed by: Day 26 playtest interpretation
C14 ledger windows (Day 9)             ← consumed by: Days 10, 23 panels and report
```

**Result:** the collapse is *shallow* — good news, and credit to Fable's ordering. Most C/D constants sit in authored content (profiles, curves, thresholds) rather than code shapes, so they can be re-tuned without refactors. The exceptions worth care:
- **C11 (socket counts) and C17 (Regolith)** feed *prefab and content assets* — re-doing them costs scene/prefab churn. These two deserve their triggers *before* their day, not after.
- **D6 (ScenarioDefinition schema)** is the only place a guess would harden into a *type*. Blank it as planned.

---

## 8. The plan's strongest moments deserve naming — credit where the plan did it right:

- **S00-style characterization-first tickets** (S00, T00). The best pattern in the plan: map what exists before moving it. This is the discovery-driven instinct, inside a locked-design plan.
- **P0-C's honest open questions** (EVA-port-vs-drop; where workPriority lives; "decide at lock") — the plan's own vocabulary for leaving blanks.
- **"Flagged for veto" entries in the Decision Log** (#13, #14, #22, #27) — Fable explicitly marked his four biggest guesses for the owner's veto. The audit's job is extending that discipline from 4 decisions to all of them.
- **Day 26's playtest question** — "what did the tester think the game was" — is a genuinely good discovery instrument.

The audit's core request is uniformity: the plan already *knows* how to leave a blank (P0-C, S00, veto list) — it just doesn't do it everywhere.

---

## 9. Deliverable 2 — Rewritten plan skeleton (rolling cadence)

Owner's stated preference kept intact: **commit each day's work one day ahead.** The change is depth-gating, not horizon:

```text
════ THE ROLLING PLAN ════

WORKING DEPTH (specified at ticket depth, 3–5 days):
  Day 1 — Scene split + clock knob + asmdefs
  Day 2 — ModuleSockets + prefab conversion (counts = current scene's needs only)
  Day 3 — S00/T00 characterization runs; staffing split T01–T02
  Day 4 — Staffing split T03–T04 (facade ≤250 lines)
  Day 5 — Transit links + pedestrian transit + voyage machine (S03)
  Day 6 — Corridor observable: farm workers walk; water workers fly

LEARNING FIELD (new, per day):
  Day 1 learns: does 1/30 vs 1/45 vs 1/60 make a day feel watchable?
  Day 5 learns: does a walking commute read at the chosen clock? does the
                voyage machine produce readable approach/hold behavior?
  Day 6 learns: does the walk-vs-fly contrast FEEL like the strategic core
                the owner described, or just like two movement types?

NEAR HORIZON (one line each; shapes only, no numbers, no schemas):
  Week 2: camera + selection + shell + read models + panels + HR v1 + presenter
  Week 3: construction content, placement, sites, corridors, allocator, tuning
  Week 4: needs/death, housing, scenario bootstrap, report, environment, session

TRIGGER-GATED DECISIONS (from DECISION_BACKLOG.md — fires when its condition hits):
  clock value → Day 1 feel test          corridor hours → Day 6 watch
  socket counts → Day 5–6 or Day 15      load rates → Day 5 watch
  Regolith → Day 14                      death timers → first needs playtest
  ScenarioDefinition fields → end of Day 21 walk-back

BACKLOG PRINCIPLE: beyond the working window, the plan holds QUESTIONS + TRIGGERS,
not designs. A day may not be committed to ticket depth until its predecessor's
learning is recorded. ("We do not specify system Q while only A and B exist.")
```

### The daily template (replaces Fable's daily format)

```text
### Day N — [title]
- Work: [tickets]
- Observable: [Press Play and see X]          ← Fable's field, kept
- Expect to learn: [Y]                        ← new field
- Decision points fed: [which backlog triggers today's results inform]
- Commit: per ticket
```

### How a day gets committed to working depth

1. Previous day's observable checked + learning recorded (one line in this doc's Day log).
2. Trigger list consulted: any trigger that fired yesterday re-scopes today.
3. The next 3–5 days get specified at ticket depth from the *current* state of the code, not the Day-0 guess.
4. Near-horizon weeks stay one-line sketches; may not gain ticket depth until they enter the working window.

**What this removes from Fable's 26 days:** D1–D7 (invented constants and layouts), C8–C24 (numbers specified before their triggers), and the "Locked" framing over C/D material. **What it keeps:** every A-class structural decision, the daily observable, the dependency-flaw discipline, and the agent-packet shape that the owner's multi-agent workflow needs.

---

## 10. Deliverable 3 — `DECISION_BACKLOG.md` (full register, ready to commit as a file)

Each entry: question → trigger → decider → current state of the space (blank).

| # | The question (blank until trigger) | Trigger | Decider | Backed by finding |
|---|---|---|---|---|
| 1 | What game-hour rate makes a day *feel* watchable? | Day 1: watch a full day + a commute at 1/30, 1/45, 1/60 | Owner (feel) | C8 |
| 2 | How long does a walk through the Pod→Farm corridor *feel* like? | Day 6: watch a commute at the chosen clock | Owner | C9 |
| 3 | How many ports/beds/nodes does each module prefab get? | Day 5–6 (ports watchable) or Day 15 (placement exists) | Owner | C11 |
| 4 | What load/unload rate feels like loading, not teleporting? | Day 5: watch a full freight cycle | Owner | D4 |
| 5 | How many ports does the Command Pod *need* to start (the player-side decision)? | Day 6: watch the shuttle queue behave | Owner | C11 |
| 6 | Which resource(s) are the first construction material(s)? | Day 14 authoring | Owner | C17 |
| 7 | What does a death timer *feel* like when it's fair? | First playtest after needs exist (Day 20+) | Owner | D1 |
| 8 | How bad is homelessness, in numbers? | First housing-shortage session | Owner | D2 |
| 9 | When should the game nag: alert thresholds? | First playtest of report screen | Owner | D3 |
| 10 | How fiddly is free placement — is 15° snap needed? | Day 15: first placement session | Owner | D5 |
| 11 | What fields does ScenarioDefinition need? | End of Day 21 walk-back from running colony | Owner | D6 |
| 12 | What belongs on the HUD at a glance? | Day 8: first unguided session | Owner | C23 |
| 13 | What columns does each panel/report need? | Day 10–11: first time you *ask* the panel a question | Owner | D7 |
| 14 | Allocator governors (priority scale, hysteresis, throttle)? | Day 18+: first allocator week | Owner | C13 |
| 15 | Ledger window/bucket/rebuild constants? | Day 9–10 observables | Owner | C14 |
| 16 | Corridor cost from length — what curve? | Day 17: first constructed corridor | Owner | C19 |
| 17 | Demolition refund policy? | Day 17: first demolition | Owner | C20 |
| 18 | Berth queue tie-break policy? | Day 5–6: first queue pile-up | Owner | C16 |
| 19 | Camera: what feels right? | Day 7: fly it | Owner | C22 |
| 20 | Site import priority? | Day 16: sites compete with colony consumption | Owner | C20b |
| 21 | Starting food stock / ice depletion timing? | Day 19–20 tuning | Owner | C11b, C11b2 |
| 22 | Water Processor automated? | Owner veto (Decision Log) | Owner | B28 |
| 23 | 6DOF integrator vs PhysX? | Owner veto (Decision Log) | Owner | A4b-adjacent |
| 23b | Placement domain ratify? | Owner veto (Decision Log) | Owner | D5-adjacent |
| 23c | Starting scenario composition ratify? | Owner veto (Decision tickets) | Owner | B29-adjacent |
| 24 | "Locked"→"PROPOSED" renaming of design docs? | At plan acceptance | Owner | C24 |
| 25 | UI Toolkit ratify? | Day 8: first screen built | Owner | B27 |
| 26 | Constitution "binding" ratify? | At plan acceptance | Owner | B31 |

## 11. Deliverable 4 — De-specification pass on the worst offenders

Rewrites of the four most over-specified artifacts, showing the pattern: **mechanism stays, guesses move to the backlog.**

### 11.1 P0-D needs/death/housing → mechanism-only rewrite

**Before (P0-D):** `hydration == 0 for 24h or nutrition == 0 for 72h → Kill`, homeless restfulness 0.5, auto-home allocator-style once per hour respecting pins, `HabitationComponent.capacity = ModuleSockets.beds.Count`.

**After (P0-D, mechanism only):**
- `ColonistNeedsComponent` (nutrition, hydration 0..1) fed from `PopulationResourceConsumer` satisfaction via the documented `Fed(colonist, resource, fraction)` seam; drains per authored rate; **consequences via existing seams only** — recovery/work multipliers through the staffing contribution path (all A-class seams). 
- **Death timing: TRIGGER-GATED — see backlog #7.** The ticket's job is the `Kill()` unwind (A9): end duty → unassign → remove from contracts/carriers → cancel transit → clear lease → record → unregister → destroy, with per-step failure logging and a result naming what couldn't be unwound. Death *fires* when needs hit 0 for an **authored threshold (value: blank pending backlog #7)**.
- Housing: `HabitationComponent.capacity = ModuleSockets.beds.Count` (A-class seam), homeless state + HR display (A), **restfulness penalty value: blank pending backlog #8**. Auto-homing policy: backlog.

### 11.2 Day 22 `ScenarioDefinition` → walk-back rewrite

**Before:** the full field list quoted in D6.

**After (Day 22 ticket):**
- Goal: the starting colony comes from data, not a hand-authored scene — proving construction/staffing creation paths are complete enough to reproduce the diorama.
- Mechanism (A-class): bootstrap through the construction completion path (Decision #28), seedable, and the hand-authored base scene content is deleted when bootstrap reproduces it.
- **Schema: TRIGGER-GATED — backlog #11.** The field list is whatever the walk-back from the running colony forces; the ticket *discovers* the schema, not specifies it.

### 11.3 Day 23 alert thresholds → plumbing + blanks

Keep the plumbing (A): rules evaluated from ledgers, allow-list, result rendering. **All thresholds: backlog #9.** Ship with authored, trivially editable defaults marked "placeholder — first session calibrates." The observable changes from "the alert reads 6.5h blocked" to "an alert fires when the Water Processor starves, with a threshold you can tune in one place."

### 11.4 P0-C placement constants → rules + blanks

Keep (A): `SitePlane` (origin+normal, never hard-code y=0), `PlacementRules` result enum, `ConstructionManager.TryPlace/Demolish` command shape, EVA-link-to-nearest-module principle. Blank (backlog): 15° snap (#10), `evaRange` (#10-adjacent / C10), site import priority (#20), the EVA-port-vs-drop open question **stays open exactly as Fable left it** — the audit changes nothing there; that's the model.

---

## 12. Deliverable 5 — Protect-list (what survives the audit untouched)

- **All A-class structure (A1–A12):** staffing split behind facade, link-graph routing, additive scenes, single voyage authority + phase machine, construction-as-composition, Kill() unwind shape, ModuleSockets, constitution rules as *rules*, S00/T00 characterization-first tickets.
- **The daily "Press Play and see" observable** — Fable's best process idea; the rolling plan keeps it and adds "expect to learn."
- **The dependency-flaw table (F1–F14)** — genuine sequencing insight, kept whole in the rolling plan's working window.
- **Publish-vs-commit, one-authority-per-fact, converter-never-counts-workers** — the load-bearing invariants of the real codebase, which the plan protects rather than touches.
- **The veto list in DECISION_LOG** — the plan's own admission that its biggest guesses need the owner's answer. The audit's uniformity campaign extends this from 4 decisions to every C/D finding.
- **P0-C's open questions** — left open, correctly.
- **Day 26's playtest question** ("what did the tester think the game was").

---

## 13. Residual risks the audit cannot de-specify

- **Review bandwidth.** A trigger-gated cadence needs the human at each trigger. With triggers consolidated into one backlog file and grouped by day, that's ~1 decision cluster per day — heavier than Fable's "review the observable and go" but that weight *is* the owner's stated method (intentional bespoke decisions). The alternative (Fable's full pre-decision) is exactly what the owner rejected.
- **Trigger timing drift.** A trigger like "first playtest after needs exist" can fire early if Day 20 slides. Mitigation: triggers name *observable events*, not days; the backlog re-scopes the next working window, it doesn't panic-schedule.
- **Agent execution drift.** Blanks are not instructions. Working-window tickets must carry "value pending backlog #N, ship with placeholder + flag" language so agents don't fill blanks silently — the exact failure the TENTATIVE banner already guards against at packet level.
- **Synthesis note for the other prongs:** when reconciling with the other two auditors, the highest-value disagreements to look for are: (1) whether the clock retune is A or B (I call the direction A and only the value C); (2) what counts as D vs C for the UI constants (I say C with very late triggers; another auditor may say D outright); (3) whether ScenarioDefinition blanking goes far enough — I kept the bootstrap *principle* (A) while blanking the schema (D6); another prong may want the principle gated too. Those three disagreements are where the synthesis has the most to gain.

---

## 14. Definition-of-done check (from the audit prompt)

1. **What are we building in the next three days, and what will we learn?** Scene split + clock knob (learning: watchability), ModuleSockets + prefabs (learning: the convention holds), characterization + staffing split (learning: the seams are where Fable says). ✅
2. **What have we explicitly refused to decide yet, and what triggers each decision?** 26 backlog rows, each with an observable trigger. ✅
3. **Which decisions survive because existing code forces them?** A1–A12, each verified against disk in the factcheck. ✅

…and the plan contains **zero** decisions about systems that don't exist without a trigger condition attached. ✅
