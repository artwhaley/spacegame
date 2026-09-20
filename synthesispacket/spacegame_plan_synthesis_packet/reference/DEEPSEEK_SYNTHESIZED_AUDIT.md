# Adversarial Plan Audit — the Blank-Space Audit of Fable's Tentative Plan

Reviewer: DeepSeek (Freebuff agent). Date: 2026-09-20.
Subject: Fable's preliminary planning review as it stands in the working tree (the
26-day plan, the root planning documents, and the packets under `tickets/`).
Method: read in the order the prompt specifies; every class-A claim verified against
`Assets/`; every class-C/D claim traced to the sentence that asserts it.

The prompt asks for five deliverables and says to write results into one file. All five
are below as **Part A–Part F**. Each part is sized to be lifted into its own file
(`AUDIT_FINDINGS.md`, `PLAN_SKELETON.md`, `DECISION_BACKLOG.md`, a de-specification
diff, `PROTECT_LIST.md`) if you want them separate. I did not create those files: they
are not mine to add to the project root.

---

## 0. Verdict, in one page

**The plan is not wrong. It is roughly 150 decisions long, and 63 of them do not
survive this audit as written — 40 of those are guesses that should have been blanks.**
The mechanical accuracy Fable earned is real, and it is exactly what makes the guesses
dangerous: every invented constant sits inside a document that has been fact-checked,
banner-marked, and cross-referenced, so it inherits authority it did not earn.

Three structural diseases, in order of cost:

1. **Guesses are framed as locks.** Five packets open with `01_LOCKED_DESIGN.md` and the
   words *"This design is fixed. Tickets implement it; they do not reinterpret it."*
   `DAY_BY_DAY_PLAN.md` has a section titled *"Locked on the spot (needed to plan; veto
   early)"*. That parenthetical is the disease stated out loud: these decisions were
   locked because the **planning document would otherwise stall**, not because the
   product needed them. And the packet's own paperwork now disagrees with itself:
   two divergent constitutions (`CORR-02`) and banners that contradict the headings
   directly beneath them (`CORR-03`).
2. **The schedule pre-pays for its own guesses.** Day 14 authors eight content assets;
   Day 18 authors the allocator's four-part algorithm; Day 22 authors `ScenarioDefinition`
   as a fifteen-field mirror of six systems that do not exist yet. Then Day 19 tunes a
   colony that has no needs, no housing and no report, and Day 26 tunes it again.
   The plan tunes twice because it built survival *after* balance (`SEQ-04`).
3. **Specificity is spent on reversible choices.** Hotkeys (`Space/1–4/F/Esc`), a button
   layout, a hover colour, a scene filename, a 15° snap, four unrelated alert thresholds.
   The meta-cost is not the effort — it is that *"locked"* appears on the same page as
   *"the player may want something else,"* which teaches every downstream agent to treat
   preference as fact.

Class tally of the 63 findings below (the tally is the audit's headline number):

| Class | Count | Meaning |
|---|---|---|
| **A — Forced** (keep, it is a fact) | 9 | A consequence of code on disk or of your stated words |
| **B — Needed now** (keep, tag a trigger) | 14 | Downstream work is genuinely blocked; no experiment could inform it |
| **C — Decidable later** (blank it, keep the seam) | 19 | Missing information is obtainable by playing or measuring |
| **D — App-shaped bullshit** (delete from tickets) | 21 | Invented because planning AIs fill blank space |

Plus, outside the tally: **4 corrections** — one of them to *this audit prompt's own seed
list* (`CORR-01`), which was not classifiable as A–D because it is a factual error rather
than a decision — and **2 verified contradictions between documents** that are defects in
the packet's own paperwork rather than design questions (`UI-04`: the camera hard-codes
the plane construction is forbidden to hard-code; `CONTENT-01`: one scenario, three bed
counts). Those five are the findings a reader can check in under a minute, and they are
why the rest of the audit should be believed.

**The one-minute answers** (the prompt's definition of done):

1. *What are we building in the next three days, and what will we learn?* — Enablers
   only (asmdefs, additive scenes, sockets, the clock field) on Day 1; prefabs on Day 2;
   the staffing split, the flight math and the port types on Day 3. Each day's learning
   is one falsifiable sentence, not a feature (Part C).
2. *What have we explicitly refused to decide?* — Needs-vs-shortage, death thresholds,
   homeless restfulness, the placement rule set, the allocator's algorithm, the scenario
   schema, the report's contents, the HR interaction model, the alert numbers, the bed
   counts, the starting stock, the starting crew split. All in Part D with triggers.
3. *Which decisions survive because existing code forces them?* — Nine, listed in Part F
   with file and line.

**A note in Fable's favour that the prompt does not anticipate.** One of the audit
prompt's own seed findings is wrong, and if it propagates it teaches the wrong lesson
(`CORR-01`). The prompt's credibility depends on it being corrected, not repeated.

---

## Part A — Findings

Format: **ID — the decision.** Where it lives · Class · Missing information · Last
responsible moment (LRM) · Replacement. Every finding is **VETO-ABLE** unless marked
KEEP. Every replacement is a seam, a measurement, a spike, or a blank — never my number
in place of Fable's.

### Corrections to this audit prompt (`CORR-*`)

**CORR-01 — The Farm's production curve is not invented; it is shipped content.**
The prompt lists *"the Farm's production curve '0.65 for one tech'"* twice (§4.1 and
§4.4) as class D. It is class **A**. `Assets/GameData/Roles/FarmOperator.asset:26`
contains `0.65`, and `StaffingEffectRule.Evaluate` (`Content/StaffingRoleDefinition.cs:33-45`)
already evaluates that curve by active-worker count with a skill bonus on top. Fable
*quoted* existing content; the prompt mistook a citation for a guess. Same error in the
same list for the housing seam: `HabitationComponent.restfulnessMultiplier` exists
(`People/HabitationComponent.cs:10`) and `HomeRestfulness` reads it
(`People/StaffingManager.cs:809-815`). **Consequence:** keep the audit prompt's instinct,
delete these two seeds, and credit Fable for the two places where he did *not* invent
(see Part F, A1/A2). A false positive on page one of an adversarial review is how reviews
lose their licence to be blunt.

**CORR-02 — There are two divergent constitutions on disk, and one of them names a type
that does not exist.** `START_HERE.md` calls `ARCHITECTURE_CONSTITUTION.md` "the
canonical copy"; `ROADMAP.md §1` reproduces the rules as a "summary" and says "if they
differ, the canonical file wins." They differ, materially:

| Rule | `ARCHITECTURE_CONSTITUTION.md` | `ROADMAP.md §1` |
|---|---|---|
| 4 (authority) | five clauses, including `ShipVoyageComponent.Flight` owns a ship's pose | four clauses; **no ship-pose clause** |
| 9 (explicit state) | writers: `DutyState`, `ShipMovementPhase`, `TransitState`, `VoyagePhase` | writers: `DutyState`, **`ShipPhase`**, `TransitState` |
| 12 (acceptance) | "plus one focused EditMode test per ticket… running the Test Runner is not a gate" | "observable behavior… not 'method returns true.'" (no test clause) |

`ShipPhase` is not a type — grep finds `ShipMovementPhase` (`Vehicles/ShipComponent.cs`).
Class · defect, not a design question. LRM: before any packet is dispatched, because
every packet README pastes "the constitution" and the two copies grade tickets
differently. Replacement: delete the copy in `ROADMAP.md §1`, replace it with a link.
One canonical file, or none.

**CORR-03 — Every banner-added document now contradicts its own headings.** The
2026-09-20 pass added `⚠️ TENTATIVE` banners to ten documents — good instinct, credit it.
But the headings underneath were not changed, so each file now says two things at once:

- `ARCHITECTURE_CONSTITUTION.md`: *"These rules are proposals; they become binding only
  after human review"* — three lines above *"The canonical copy… if they disagree, this
  file wins"* and *"Every ticket is checked against these rules."*
- `GAME_DESIGN_DECISIONS.md`: *"Nothing here is locked"* — directly above `## Locked` and
  *"Locked decisions are binding on all packets."*
- `HOW_IT_WORKS.md`: *"This describes the minigame as proposed"* — with §2 narrating a
  day in a colony that has never been played, as though observed.

Class · **D** (framing), and it is the most consequential D in the packet, because the
banner is a *promise* that the body then breaks. LRM: today. Replacement: the banner
becomes the only voice. `## Locked` → `## Proposed — binding once you say so`;
`01_LOCKED_DESIGN.md` → `01_WORKING_DESIGN.md`; "this file wins" → "the only copy of
this list." If a heading still claims authority, the banner is decoration.

**CORR-04 — The plan resolves an open question inside a ticket, and its own table forbids
that.** `GAME_DESIGN_DECISIONS.md` §Open says *"Resource chains beyond Ice→Water→Food;
which construction material(s) exist first?"* and the file header says **"do not resolve
inside a ticket."** Day 14 resolves it: `Regolith`, one material, no fabrication chain
(`DAY_BY_DAY_PLAN.md` F5; DECISION_LOG #23; `P0-C §Content`). Class · **D**. The
resolution is probably right, which is the point — "probably right" is not the same as
"decided by the person who owns the question." LRM: Day 14 (i.e. it should be *asked* on
the day the rock deposit is authored, when you can see whether one material makes building
feel like an economy). Replacement: Day 14 authors exactly one material because
`BuildingDefinition.cost` needs a `ResourceDefinition` instance, and the ticket says so.
The *chain* question stays open with trigger "when a second construction job has been
queued and both feel interchangeable."

### 1. Invented constants (`CONST-*`)

**CONST-01 — Death thresholds: `hydration == 0` for 24h, `nutrition == 0` for 72h.**
`tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md §Needs`; restated as "a hard threshold →
death" in `GAME_DESIGN_DECISIONS.md §People`; narrated in `HOW_IT_WORKS.md §2`
("Zero for long enough → … the one kill command"). Class · **D**. Missing information:
what a death should *feel* like from the player's chair — how much warning is enough to
act on, and whether the failure mode is one dramatic death or a slow thinning. Nobody has
watched a colony starve; there is no number in the codebase to derive it from
(`PopulationResourceConsumer` today consumes `residents × rate × delta` and records only
an aggregate `ShortageActive` flag — `People/PopulationResourceConsumer.cs:32-45,74-88`).
**LRM:** after the first playable (Day 26-equivalent), and specifically after one run in
which food runs out. Replacement: **blank.** Ship the meter and the warning, not the
timer: `ColonistNeedsComponent` exposes `nutrition`/`hydration` and one authored drain
rate; when a need first enters a warning band, write `ReadinessHistory("need.critical")`
once with a transition (not per tick). No threshold, no timer, no death. Add a backlog
question with the trigger *"when you have watched the meter empty and felt nothing"* —
because if the meter emptying does not produce anxiety, the fix is not a shorter timer,
it is a louder meter, and a timer would have hidden that. See also `ENT-01`: this finding
presupposes needs are personal at all, which is itself open.

**CONST-02 — Homeless restfulness `0.5`.**
`P0-D §Housing` ("Homeless colonists (home null or over capacity) sleep at their current
location at restfulness 0.5"); `GAME_DESIGN_DECISIONS.md §People` ("homeless colonists
rest badly"); Day 21's observable ("8 colonists, 6 beds → two rest badly → build a
Habitat → fixed"). Class · **D** for the value, **A** for the seam. Verified: the seam is
real (`HabitationComponent.restfulnessMultiplier`, `StaffingManager.HomeRestfulness`),
**and `HomeRestfulness` returns `1f` when `home == null` today** (`StaffingManager.cs:809-815`).
So the plan is not preserving behavior — it is silently nerfing an accidental path with
an invented number, and the nerf is invisible because the seam already existed. Missing
information: whether a bed is a soft tax, a hard blocker, or a death clock. LRM: the day
beds first become the binding constraint (Habitat exists), not Day 21's schedule slot.
Replacement: keep the seam, ship `1.0` (today's behavior), and rewrite Day 21's
observable to what the day actually proves: *"Place a Habitat; the two colonists with no
bed are named as unhoused in HR; nothing else changes."* Backlog the number, trigger
*"when you have watched a colonist sleep somewhere you did not intend."*

**CONST-03 — `15°` rotation snapping.**
`DAY_BY_DAY_PLAN.md` §"Locked on the spot"; `GAME_DESIGN_DECISIONS.md §Placement and the
world`; DECISION_LOG #22. Class · **D**. Missing information: whether rotation granularity
matters *at all* in a world with no terrain, no grid, no vertical stacking, and no
footprint interlock beyond bounds overlap. Nobody has held a ghost. LRM: the hour the
ghost first becomes draggable. Replacement: `PlacementRules` accepts the pose it is
given. Rotation snapping becomes a `PlacementSettings` field, authored *continuous* by
default. This is one inspector field and one `Mathf.Round`, and it is strictly less work
than deciding it now.

**CONST-04 — Alert thresholds: food < 12h, colonist blocked > 2h, port holding > 1h, site
starved > 8h.**
`DAY_BY_DAY_PLAN.md` Day 23; `tickets/P0-B_Interaction_UI/01_LOCKED_DESIGN.md §7`. Class ·
**D**, and it is four independent guesses bundled into one bullet, which is how a guess
becomes a spec. Missing information: the *distribution* of each metric in a normal run.
You cannot pick "blocked > 2h" without knowing whether normal blocked time is 20 minutes
or 6 hours — and Day 19 is the only day that could produce that number, five days later.
LRM: after the first 72h run. Replacement: `AlertRules` becomes a **data table**
(`AlertRuleDefinition`: metric, threshold, hysteresis, message) shipped with **empty
thresholds** plus a `[ContextMenu] Dump last 24h` that prints each metric's p50/p95/max
from the ledgers. Tuning then sets thresholds from observed data. Four invented constants
become one measurement step, and the work is *less*.

**CONST-05 — "48 hours of Food" starting stock.**
`GAME_DESIGN_DECISIONS.md §Starting scenario`; `DAY_BY_DAY_PLAN.md` §"Locked on the spot";
`HOW_IT_WORKS.md` intro; DECISION_LOG #27. Class · **D**. Missing information: what the
farm actually yields at the starting staffing under the new clock — and the plan schedules
the *only* thing that could tell you (Day 19 tuning) **seven days after** it authors the
number for Day 22. LRM: Day 19. Replacement: the scenario's `startingStock[]` is authored
at Day 22 from the Day 19 measurement, and before Day 19 the scenario simply carries
*whatever the current scene holds* (a class-A copy, not a design). One sentence in the
ticket; zero guessed units.

**CONST-06 — The starting crew: 8 colonists, 3 Pilots / 2 Farm Technicians / 3 Builders.**
`GAME_DESIGN_DECISIONS.md §Starting scenario`; `HOW_IT_WORKS.md` intro; DECISION_LOG #27;
`DAY_BY_DAY_PLAN.md` §"Locked on the spot" (which says **6 beds** for the same pod —
see `CONTENT-01`). Class · **D**, and it is the most consequential guessed constant in the
plan, because it decides what the game is *about* on minute one: whether pilots are scarce
(2 ships, 3 pilots is a deliberate over-staff), whether construction is visible (3
Builders), whether the farm can keep up (2 techs). Missing information: the Day 19 run.
LRM: Day 19/22, not before. Replacement: the scenario's `colonists[]` list is a **tuning
artifact** and the plan should say those words. Day 22's committed work is "the scenario
spawns *whatever crew the asset names*"; the numbers are set on Day 22 from the Day 19
observation, with the *classes* required (they are a scenario-schema input) and the
*counts* blank.

**CONST-07 — `traversalHours = 0.25` for the CommandPod↔Farm corridor.**
`tickets/P0-A_Foundation/01_LOCKED_DESIGN.md` (§Part 2, scene content) and
`T07_H3_COMMUTE_INTEGRATION.md` step 4; also the field default. Class · **C** (the field
is the right seam; the value is a balance statement). Missing information: whether a
commute should cost 3% of a shift or 30%, which is a claim about how *visible* the
walk-vs-shuttle trade is — the plan's own genre thesis. LRM: Day 6's acceptance (Fable's
Day 6; Day 5 in Part C). Replacement: author it, mark it **"placeholder — value is the
question"** in the ticket, and put the question in the backlog with the trigger *"when you
have watched a worker walk a corridor and had an opinion about the pace."* Note the
plan's `0.25` is quoted from the code default and reads like a fact; a one-word
annotation fixes that.

**CONST-08 — `loadGameHoursPerUnit = 0.002` (≈7 game-s) and `loadGameHoursPerPassenger =
0.01` (36 game-s).**
`tickets/P0-S_Shuttle_Flight/01_LOCKED_DESIGN.md §4`. Class · **D**, and it is the most
*self-defeating* guess in the packet: the stated design goal three lines above is
*"you can watch the number tick"* (`HOW_IT_WORKS.md §2`), and the only way to know whether
a number ticks visibly at 1× and 10× is to watch it. LRM: the day the phase exists.
Replacement: author `0` and **require the observable to fail first.** Day 5's acceptance
should read: *"cargo moves instantly; the loading phase is invisible; this is wrong —
now pick a number and re-run."* A blank that fails loudly is strictly better than a guess
that passes invisibly, and here the failure is free.

**CONST-09 — Flight profile numbers.**
`P0-S §2`: `massKg 20000`, `mainThrustN 60000`, `rcsThrustN 8000`, `rcsTorqueNm 40000`,
`inertiaKgM2 150000`, `cruiseSpeedMax 40`, `approachSpeedMax 3`, `undockPushDistance 15`,
`substepSeconds 0.5`, `maxSubstepsPerTick 200`. Class · **C, and this is the packet's best
moment** — Fable puts the numbers in a **content asset**, derives PD gains from them
("so content authors tune physical numbers, not PD constants"), and labels them
*"placeholders; tune for look, then report them"* (`P0-S README`). That is exactly the
right treatment and it should be the template. The finding is narrow: **the docking
tolerances were left out of it.** `captureDistance 0.3`, `captureSpeed 0.5`,
`captureAngleDegrees 5` live on `DockingPortComponent` (`P0-S §1`) — a *Runtime* component
— so three numbers that are really *"how precisely can this vehicle dock"* get authored
per-port in the scene, where they look like level design instead of like ship tuning.
`captureAngleDegrees = 5f` in particular is a gameplay constraint ("you must be this
aligned") wearing a physics number's clothes. LRM: Day 3 (when `ShipFlightProfile` is
created). Replacement: move the three tolerances into `ShipFlightProfile`. Same work,
one tunable place, and "the ship is clumsy" becomes a content sentence instead of a
docking-port edit.

**CONST-10 — `maxRangeMeters` and the deposit-selection heuristic.**
`DAY_BY_DAY_PLAN.md` Day 14; `EXPLORATION_AND_LONG_RANGE.md §8`. Covered in full at
`ENT-05`; listed here because the *number* is also a guess. Class · **D**.

**CONST-11 — P0-A's size targets (facade ≤ 250 lines, extracted classes ≤ 300).**
`P0-A §"Target sizes"`, asserted as acceptance in `T08` ("`StaffingManager.cs` ≤ 250
lines"). Class · **B**, with a caveat worth one line: a *ceiling* is honest, a *target* is
a number an agent will contort code to hit. `1,063 → ≤250` is a 4× claim made before the
split's responsibilities are known (`T00` exists precisely to discover them). LRM: Day 3,
after `T00`. Replacement: acceptance is `no file > 300 lines` (measurable, and the
constitution's own rule 10 is ~400); "target 250" becomes advisory prose. Low severity,
but it is the difference between a ticket that fails on behavior and one that fails on a
number.

**CONST-12 — Reversible one-line choices consuming plan budget.**
`P0-D §Session` (hotkeys `Space`/`1–4`/`F`/`Esc`; `Assets/GameData/Scenarios/Minigame.asset`);
`P0-B §1` (pivot on `y = 0`, threshold values); Day 23 (a sparkline of 24 buckets);
`P0-S §1` (`captureAngleDegrees`, again). Class · **D**, meta. Missing information: none —
these will be decided by feel within a minute of touching them. LRM: when touched.
Replacement: cut them from tickets; they are implementation detail. The cost is not the
writing: it is that a reader comparing *"hotkeys are Esc menu"* against *"the placement
domain is a `SitePlane`"* sees both in the same declarative register and concludes the
plan knows both. Specificity is a budget, and this is where it leaks.

**CONST-13 — The clock interval itself (`[1/60, 1/30]` game-hours per real second).**
`tickets/P0-S_Shuttle_Flight/01_LOCKED_DESIGN.md §0`; `P0-0 K03`; DECISION_LOG #14
(flagged for veto); `GAME_DESIGN_DECISIONS §Time and shifts`. Class · **B** — and this is
the plan handling a guess *correctly*: he flags it, gives a range instead of a point,
states why ("at 1 h/s nothing is watchable"), and notes that every rate is per game-hour
so nothing else changes. Credit. Two findings remain. (i) **The change is testable in
minutes and the plan spends a day on it** (Day 1 retune, `K03`): expose the field, change
it in Play Mode, look. `PREF-03` makes the same argument about the UI stack. (ii) **Every
downstream constant is then authored against the feel of watching**, which is the one
thing that can validate the clock — `traversalHours`, `loadGameHoursPerUnit`,
`substepSeconds`, "72-game-hour run at 10×", Day 26's "15 minutes". Missing information:
whether the number *survives* one hour of watching. LRM: Day 1, on the day. Replacement:
keep the seam and the range; add one **protect-list rule** so the guess cannot metastasize:
*no constant in this plan is expressed in real seconds; every duration is authored in game
hours and only the clock's single multiplier is real time.* That rule is what makes the
clock cheap to change (and it is why `CONST-08`'s load times matter). Under that rule,
changing the clock is one inspector field forever, exactly as the plan claims.

### 2. Invented entities (`ENT-*`)

**ENT-01 — `PopulationResourceConsumer.Fed(colonist, resource, fraction)`: needs as a
personal fact.**
`P0-D §Needs`: *"`PopulationResourceConsumer` reports per-colonist satisfaction each hour
(`Fed(colonist, resource, fraction)`)".* Class · **D**, and **the single most consequential
invented entity in the plan.** Verified against `Assets/Scripts`: `PopulationResourceConsumer`
is a *habitation-level* component. `SimulationTick` iterates `entries`, computes
`residents × amountPerResidentPerGameHour × delta`, calls `inventory.Remove`, and stores
only `requestedLastTick` / `consumedLastTick` / `shortageActive` on the entry
(`People/PopulationResourceConsumer.cs:74-93`, `:25-45`). It **cannot name a colonist** and
its `ResidentCount` comes from `HabitationComponent.ResidentCount`. Adding `Fed()` is not
a seam — it is a rewrite of the consumption model from aggregate-at-a-place to
personal-at-a-person. Missing information: **whether needs should be personal at all.**
Banished models this as aggregate shortage; `HOW_IT_WORKS.md` narrates it as
*"the draw fails, and each colonist's needs start falling"* — i.e. Fable quietly chose the
personal model in a walkthrough, and then wrote it into an API. This decision reshapes
Days 11, 20, 21, 23 and 25 (`SEQ-02`). LRM: **before Day 11**, because Day 11's colonist
panel reserves two need bars. Replacement: **blank, and leave the working code alone.**
`PopulationConsumptionEntry.ShortageActive` already says "this place is short" — surface it
(a `ReadinessHistory("consumption.shortage")` transition), put the two reserved bars in the
colonist panel as *"—"*, and backlog the question with the sharpest trigger available:
*"when a shortage happens and you want to know **who** is hungry, rather than that the
colony is."* If you never want that, the personal model was never needed, and the plan
would have paid a rewrite for a walkthrough sentence.

**ENT-02 — A colonist-level work multiplier folded into `StaffingComponent`
contribution.**
`P0-D §Needs`: *"colonist work multiplier … exposed as `ColonistStatusComponent.WorkEffectiveness`;
`StaffingComponent` multiplies its per-worker contribution by it (counts stay integer;
contribution scales)"*; repeated at `ROADMAP §D` and DECISION_LOG-adjacent prose. Class ·
**D**. Verified: no such input exists. `StaffingComponent.PublishPerformance` calls
`snapshot.Multiply(rule.effect, rule.Evaluate(activeScratch))`
(`People/StaffingComponent.cs:145-177`), and `StaffingEffectRule.Evaluate` is
`CurveAt(count) × (1 + avgSkillBonus)` — the curve's argument is a **count**, and the only
per-worker scalar already folded in is *skill*, applied to the whole value
(`Content/StaffingRoleDefinition.cs:20-45`). "Multiplying the per-worker contribution" is
not expressible today. Missing information: **the aggregation semantic.** When one of
three workers is impaired, does the facility run at (a) the *average* effectiveness,
(b) the *worst* worker's, or (c) a *fractional headcount* fed into the existing count
curve? Those are three different games — (c) is the simplest implementation and also the
only one that reuses `CurveAt`, but it forces `CurveAt(int)` to `CurveAt(float)` and
therefore re-validates every authored curve in `Assets/GameData`. LRM: before Day 20.
Replacement: **blank.** Do not invent `WorkEffectiveness`; write `ENT-01`'s answer first,
then ask this question in the backlog with trigger *"when a colonist is impaired and you
notice (or don't notice) the facility slowing."* Note the plan's phrase "counts stay
integer; contribution scales" *sounds* like it has already resolved the semantic. It has
not; it has hidden it.

**ENT-03 — `ColonistNeedsComponent` on the colonist prefab.**
`P0-D §Needs`. Class · **C** (the right *home* for the fact, if the fact exists). Finding:
it creates a **second owner of "is anyone short."** After it lands, both
`PopulationConsumptionEntry.ShortageActive` (per place) and `ColonistNeedsComponent`
(per person) can answer a question that sounds identical, which is a rule-4 violation
waiting to drift. LRM: after `ENT-01`. Replacement: the seam is *one* of the two, never
both. If needs stay aggregate, `ColonistNeedsComponent` is deleted and the colonist panel
reads the habitation's shortage. If needs become personal, `ShortageActive` becomes a view
derived from the personal state. Say which in the backlog, not in `P0-D`.

**ENT-04 — `PopulationManager.Kill()`'s fixed eight-step unwind order.**
`P0-D §Death`: *"end active duty (`Died`) → `StaffingManager.Unassign` → remove from any
`TransportContract.passengers` and `PassengerCarrierComponent` → `Pedestrian.Cancel()` →
clear ship `ResponsiblePilot` … → `ReadinessHistory("colonist.death")` → unregister →
destroy."* Class · **B** for the command (F12 is real), **C** for the fixed order. Two
verified corrections that make this sharper than the plan:

- **Two of the eight steps already happen.** `ColonistAgent.OnDestroy` unregisters from
  `PopulationManager` and `StaffingManager` (`People/ColonistAgent.cs:136-143`), and
  `PopulationManager.Unregister` is a list removal (`People/PopulationManager.cs:64-68`).
  So an acceptance test written as *"the registries no longer contain them"* **passes
  today, before `Kill()` exists**, and proves nothing.
- **The real gaps are the ones that do not self-clean**: `TransportContract.passengers`,
  `PassengerCarrierComponent.currentPassengers`, `ShipComponent.ResponsiblePilot`, any
  open `BlockedTimeLedger` interval (once Day 9 lands), any pinned-set in the allocator
  (once Day 18 lands), and the *death record*, which by definition cannot live on the
  object being destroyed. Fable's ordering already gets the record-before-destroy
  sequencing right — credit that.

Replacement: express the ticket as an **invariant plus an adversarial test**, not a
sequence: *"`Kill(c)` returns only when no contract lists `c`, no carrier holds `c`, no
ship holds `c`'s lease, no ledger interval with `c` as subject is open, and the report can
name the cause. The order is whatever achieves that."* Then the test names the holders
(the plan's own `F12` list, plus the two the plan will have added by Day 20). This is
strictly *less* specified and strictly stronger, and it fails loudly on the ninth holder
instead of requiring a ticket to stop and report. If a "ninth holder" is genuinely
discoverable by grep on the day, say so in the ticket — that is the whole point.

**ENT-05 — The deposit registry + `maxRangeMeters` + "highest uncovered foreground
demand."**
`DAY_BY_DAY_PLAN.md` F6 and Day 14; `EXPLORATION_AND_LONG_RANGE.md §8`; DECISION_LOG #24.
Class · **D** for the selection algorithm, **B** for the underlying need. Verified: the
`targetDeposit` is a single authored field on a per-ship controller
(`Extraction/ExtractionMissionController.cs:20`), there is no deposit registry, and
`maxRangeMeters` does not exist. The *need* is real: the slice wants `Regolith` and the
one mining ship points at ice. But Fable bundled "we need a second deposit" with a
**fully-specified autonomous selection heuristic** — a registry, a range constant, a
demand-ranking rule, and a nearest-with-stock fallback — none of which anyone can evaluate
before a player has wanted a specific rock. This is also the plan's clearest case of
*taking a decision away from the player* to avoid authoring a command; the alternative
("send the miner here") is one UI line and one existing pattern. LRM: Day 14. Replacement:
Day 14 authors the `Regolith` resource and a rock deposit in the scene, and the mining
controller keeps its single `targetDeposit`. The seam is `EXPLORATION §8`'s own instinct —
keep the *reference* a reference — plus a backlog question: *"the miner points at one
deposit. Is choosing a deposit a player decision (a command from the deposit panel) or an
automatic one (a ranking rule)? Decide when there are two rocks and you want one."* Note
`EXPLORATION §8`'s rationale for the registry is *future* discovery ("new deposits then
work the moment they are discovered"), which is a Phase 1 concern; the Phase 0 *seam* it
actually needs is that extraction reads a reference rather than a hard-coded position,
which it already does.

**ENT-06 — `ScenarioDefinition`, fifteen fields deep.**
`P0-D §Scenario`: `sitePlanes[]`, `modules[] {BuildingDefinition, pose, planeId,
initialStock[], staffingTargets[]}`, `corridors[] {moduleA, nodeA, moduleB, nodeB}`,
`ships[] {prefab, dockModule, portIndex, flightProfile}`, `deposits[] {prefab, position,
amount}`, `colonists[] {name, classes, skills, home, employment?}`, `startHour`,
`clockDefaults`. Class · **B** that a scenario asset exists (F13 is real and Fable's
ordering fix is correct), **D** for essentially every field. The schema is a *mirror of
the current shapes of six other systems*, so it inherits all six systems' guesses:
`staffingTargets[]` is a P0-C concept, `corridors[]` carries `nodeA/nodeB` indices from
P0-0's `ModuleSockets`, `ships[]` carries `portIndex`, `modules[]` carries `planeId`
(`ENT-12`). Every one of those is a C/D finding elsewhere in this audit, which means this
one entity is the plan's dependency nexus (`SEQ-03`). Missing information: **what a
"module instance" is once `ConstructionManager` owns placement** — probably nothing more
than a definition plus a pose. LRM: Day 22. Replacement (proposal, veto-able, and the
highest-leverage structural change in this audit): **do not hand-author the schema at
all.** Make Day 22's work be a command on the construction manager that walks the live
scene and **writes** a `ScenarioDefinition` asset from what is there; `New Game` replays
that asset through the same creation path it was captured from. The schema then becomes
whatever the writer emits — correct by construction, unable to diverge from construction
(which is DECISION_LOG #28's own stated reason for the bootstrap), and the human's veto
becomes one question: *"did the reloaded colony look like the one I built?"* This deletes
~15 invented fields and converts a design decision into a mechanism. `ScenarioBootstrap`
as a *consumer* stays; the *hand-authored default asset* is what goes.

**ENT-07 — `BuildingDefinition`'s field list and unlocked-flag.**
`P0-C §Content`: `displayName`, `completedPrefab`, `cost: ResourceAmount[]`, `buildHours`,
`footprint: Bounds`, `category`, `isCorridor`, `maxCorridorLength`, `evaRange`,
`unlocked = true`. Class · **B** for definition/prefab/cost/buildHours (Days 14–17 need
them), **C** for `footprint`/`maxCorridorLength`/`evaRange` (they are placement-domain
decisions in disguise — `ENT-12`), **D** for `category` and `unlocked` (invented fields
with no behavior: there is no lock, no unlock, and no category consumer in the slice).
LRM: Day 14. Replacement: author the four fields the construction path touches. Add
`category`/`unlocked` on the day something reads them; a field with no reader is a claim
that a feature exists.

**ENT-08 — `SitePlane` and `planeId`.**
`DAY_BY_DAY_PLAN.md` §"Locked on the spot" + Day 15; `P0-C §Placement`;
`GAME_DESIGN_DECISIONS.md §Placement`; DECISION_LOG #22; `EXPLORATION §8`; `GLOSSARY.md`.
Class · **D**, and it is the plan's deepest over-specification. It is a complete placement
model for a game nobody has played, decomposed into an abstraction whose *only*
justification is a Phase-2 outpost ("planes generalize to outposts") that does not exist,
with an id field (`planeId`) that no consumer reads. `DAY_BY_DAY_PLAN.md` even tells the
implementer *"Never hard-code y = 0"* — while `P0-B §1` hard-codes the **camera** pivot at
`y = 0` (`UI-04`). LRM: Day 15, on the day. Replacement: **one plane, no id, no
abstraction.** A `SitePlane` value type with origin+normal is fine *if* it costs nothing —
but the ticket must not carry `planeId`, must not carry "corridors never cross planes"
(a rule for a world with one plane), and must not carry an outpost rationale. `ENT-12`
covers the rule set. The generalization belongs in the backlog with the trigger *"when a
second base is actually wanted."*

**ENT-09 — `WorkforceAllocator`'s algorithm: tick priority 90, hourly throttle, four-step
fill/release, and 4-game-hour hysteresis.**
`P0-C §Staffing targets`; `ROADMAP §C`; DECISION_LOG #6. Class · **A** for the *control
model* (targets + priority + pin is **your** choice, per DECISION_LOG #6's own "User
choice"), **A** for priority 90 (it must fill before staffing plans the shift — a real
ordering constraint), **D** for everything else. Four invented behaviors for the most
player-visible system in the game, written before a single target has been set by hand:
the fill order ("descending priority, excluding colonists whose current workplace has
higher priority"), the release rule ("unassign the most recently assigned non-pinned
worker"), the throttle, and the hysteresis window. Missing information: **whether the
allocator should ever move a worker.** Releasing above target means a player who reduces a
target watches someone get reassigned mid-shift — that is a *feeling*, and it is
unjudgeable before it happens. LRM: Day 18, and specifically **after** the manual HR
screen has been used for real. Replacement — split the day, which is the restructuring
this audit recommends most often:

- **Day 18a (commit): `StaffingTarget` is data.** `SetTarget(role, shift, n) →
  TargetResult`, `workPriority`, a pin flag, and an HR column that shows the **gap**
  ("Farm Operator / Shift B — assigned 1, target 2") with **no autonomous behavior at
  all.** This is genuinely needed (nothing else can show the gap) and it cannot be wrong.
- **Day 18b (backlog, trigger-gated): the allocator.** Trigger, in your words, would sound
  like: *"you hand-assigned a shift change twice and wished you hadn't."* Then the only
  remaining questions are release policy and thrash-avoidance, and you will answer them
  by watching, not by reading a draft.

Note the bonus: today the plan's Days 20, 21 and 23 *depend on the allocator in their
observables* ("allocator refills if anyone is eligible", "allocator/HR show homeless
count"). Deferring costs three sentences rewritten to "assign it by hand" — cheap, and
`SEQ-05` maps it.

**ENT-10 — `StaffingComponent.ActiveWorkersChanged` (the one Runtime change for
presentation) + `ReadinessHistory.OnRecorded`.**
`P0-P §Design` / `PACKET_INDEX` seam list; `P0-B §3`. Class · **B/A**: both are real seams
for real needs (`OnRecorded` verified needed: `ReadinessHistory.Record` is fire-and-forget
to JSONL, `Core/ReadinessHistory.cs:58`). Only finding: `P0-B §4` has three ledgers that
**each subscribe to `OnRecorded` *and* tick at priority 900**, so "when a bucket closes"
has two mechanisms. Pick one (event-driven for intervals, tick for rotation) and say so in
the ticket, or Day 9 will produce double-counted buckets on the first `Holding → Blocked →
Holding` sequence. Low severity, high certainty.

**ENT-11 — `ModuleSockets`' eight socket categories, authored on Day 2 for consumers that
arrive on Days 5, 13, 15 and 16.**
`tickets/P0-0_Skeleton/00_README_AND_TICKETS.md K04`; DECISION_LOG #21. Class · **A** for
the *convention* (`F3` is real: five later tickets would each invent their own transform
naming — ports for P0-S, slots for P0-P, nodes for P0-C, mesh-root separation for
`ShipView`) and **D** for the *list*. `meshRoot`, `attachmentNodes`, `dockPorts`,
`dockApproaches`, `holdingSlots`, `workstations`, `beds`, `evaSpawn`, plus an `OnValidate`
rule about bed counts — eight guesses about consumers that do not exist, one of which
(`beds`) is actually the *economy's* bed capacity (`CONTENT-02`) and another of which
(`attachmentNodes`) is a placement-domain decision in disguise (`ENT-12`). LRM: Day 2 for
the convention, each consumer's own day for its categories. Replacement: Day 2 authors
`meshRoot` + `dockPorts` + `dockApproaches` (Day 5's needs); `workstations`/`beds` land with
P0-P; `attachmentNodes`/`evaSpawn` land with P0-C. Adding a field on the day its consumer
arrives costs nothing; guessing it on Day 2 and discovering on Day 16 that the EVA spawn
wants to be a *link pair* rather than a *point* costs a scene re-authoring pass.
**This is a class-A-adjacent finding and the most tempting to skip** — which is why it is
worth stating: the convention is genuinely good, and the *list* is the part that would have
to be paid for.

**ENT-12 — The placement rule set: eight results, node-pair snapping with a free-placement
fallback, straight same-plane corridors with a max length and no footprint intersection.**
`tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md §Placement` (`{Valid, Overlaps, OffPlane,
CorridorTooLong, CorridorNotStraight, CorridorIntersects, NoNodeInReach, Locked}`);
`DAY_BY_DAY_PLAN.md` §"Locked on the spot"; `GAME_DESIGN_DECISIONS.md §Placement and the
world`. Class · **D.** Missing information: **whether snapping or free placement is the
interaction.** The plan says both — *"snap to the nearest free `attachmentNode` pair within
snap radius **else** free placement at 15° steps"* — which is neither, and the resolution is
a mouse question answered in a minute of dragging. Meanwhile eight result values are
enumerated for a ghost that does not exist, and the corridor rules are rules for a feature
(Day 17) two days further out. LRM: Day 15, on the day, ghost in hand. Replacement —
derived from the schedule's own observables, not preferred: `{ Valid, Overlaps }`, because
Day 15's observable says only *"it snap[s] to nodes and turn[s] red on overlap."* Add
`NoNodeInReach` if snapping wins the drag test. Everything else — corridor straightness,
max length, intersection tests, `Locked` (a lock feature that exists nowhere) — is backlog,
and corridors get their rules on Day 17 when a corridor is the thing on screen. Note also
what the plan *already* got right and should keep: `PlacementRules` as a pure function of
(definition, pose, footprints) with `TryPlace` returning a result enum is the correct seam
shape; the seam is the *signature*, not the contents.

**ENT-13 — Holding slots: `slot index = queue position mod slots`, with one slot authored
per station.**
`tickets/P0-S_Shuttle_Flight/01_LOCKED_DESIGN.md §1` (`HoldingSlotFor`) and §6
(`CommandPod 2 holding slots, Farm 1, Water Processor 1`); `P0-0 K05` authors the same
counts on the prefabs. Class · **C**, and unlike most C findings this one has a **verified
broken case**: with one holding slot and two ships queued, modulo arithmetic places both at
the same pose, so the *design* says two ships overlap. Day 6's own observable
(*"Duplicate the shuttle → one holds at the single Farm port"*) creates exactly that state
with the second ship arriving as a stranger. Missing information: what a queue of more than
one should look like — a computed offset, a line, a stack, or a `Blocked`. That is a
question about reading a queue at a glance, and it is answered by watching two ships. LRM:
the moment a second ship queues (Fable's Day 6; Day 5 in Part C). Replacement: author one
slot, let `HoldingSlotFor` return null beyond the authored count, and have the ticket report
what actually happened rather than what modulo says. Add the question to the backlog (B-11).
This is the audit's cleanest example of *"a precise rule is less rigorous than a blank"*:
the modulo formula looks like engineering and silently hides a visual defect.

### 3. Invented UI (`UI-*`)

**UI-01 — HR v1/v2 layout: a two-pane person/workplace tree with candidate lists and
per-candidate rejection reasons.**
`DAY_BY_DAY_PLAN.md` Day 11 and Day 18; `P0-B §6`; `U06` observable. Class · **D**, and it
matters more than any other UI finding because **this is the interface to the strategic
core.** The plan decides — before one HR screen has been looked at — that assigning work is
a left-right tree: colonists on the left, workplaces × roles × shifts on the right, a
candidate list with `AssignmentResult` reasons under each row, filters (eligible / blocked
/ off-shift), and a status line echoing `Describe(result)`. Missing information: **how the
player thinks about the question.** "*Who should work here?*" (job-first, the plan's
model), "*What should this person do?*" (person-first), and "*How many people should be
farming?*" (number-first) are three different screens. The plan commits to job-first *and*
simultaneously commits to automating it (DECISION_LOG #6), which means the screen it
designed in detail is the mode it intends to make optional. LRM: Day 11 — the day it is
drawn. Replacement: commit only the **data** (the colonists, the roles, the assigned/cap
counts, the state) and **one** interaction (assign/unassign from a selected row). Backlog
the interaction model, trigger: *"you have run one full shift change by hand."* For the
record, the *reason strings* are the best part of this design — `RejectedMissingClass` next
to a candidate is exactly rule 3 done well. Keep that; cut the tree.

**UI-02 — The colony report's five sections and its tables.**
`DAY_BY_DAY_PLAN.md` Day 23; `P0-B §7`; `U07`. Class · **D**. Population summary;
resources table with 24-hour in/out/net AND hours-until-empty AND a **sparkline of 24
buckets**; blocked-time table; duty-failure sentences; transport table with average wait
and holding per port. Missing information: **who reads it, and for what.** The plan's own
Phase 0 definition of done is *"a stranger plays 15 minutes unaided and can say what the
game is"* — a stranger does not read a table. The report actually has two users with
opposite needs: the **lose screen** wants one sentence, and the **developer** wants
everything. Building the developer's screen for the lose screen's job means Day 25 embeds
a dashboard in a game-over panel. LRM: Day 23. Replacement: Day 23 commits **one screen
with one section** — *"what went wrong"* — built from the last few `colonist.death` /
`*.blocked` / `consumption.shortage` events as plain sentences, which is exactly the
observable the ticket already promises ("read 'Water Processor: no eligible pilot…' in
plain English" — that sentence is achievable without a single table). Backlog the tables,
trigger: *"you lost a run and could not tell why from the one screen."* Alert rules are
already separate (`CONST-04`).

**UI-03 — Colonist panel reserves two need bars nine days before deciding whether needs
exist.**
`DAY_BY_DAY_PLAN.md` Day 11 ("needs (placeholder until Day 20)"); `P0-B §5`
("needs bars (hidden until P0-D)"). Class · **D** as sequencing, and a clean example of
`SEQ-02`: the UI is authored first, so the UI's shape becomes a constraint on the model.
LRM: Day 11. Replacement: no bars. Add them on the day `ENT-01` has an answer. A panel
that shows a reserved empty space for a system nobody has designed is a *drawing of a
decision*, and the cost of removing it later is higher than the cost of adding it.

**UI-04 — The camera hard-codes the plane the construction system is forbidden to
hard-code.**
`P0-B §1`: *"pivot on the y = 0 plane"*. Compare `DAY_BY_DAY_PLAN.md` Day 15: *"Never
hard-code y = 0"*, and `EXPLORATION §8` item 2. Class · **C**, and a **verified internal
contradiction**. Missing information: none really — it is a small consistency bug with a
design smell attached. LRM: Day 7. Replacement: the camera pivot is a point the player
sets (a focus target or a ground-less orbit target); the phrase "the y = 0 plane" leaves
the plan entirely.

**UI-05 — "Full-screen modal" HR and Colony Report.**
`P0-B §3`. Class · **C** (a preference presented as a layout fact). Missing information:
whether you want to stop watching the colony to manage it. The plan's own thesis is that
ships flying is what the player *watches* (`GAME_DESIGN_DECISIONS §Identity`). A modal
means the answer to "*why is nobody loading the shuttle?*" requires closing the screen
that would show you. LRM: Day 11. Replacement: keep the modal for v1 (it is easier) but
write the tension down in the backlog: *"HR is a modal. Is management something you do
*to* the colony or *while watching* it?"* One sentence now, a real design fork later.

### 4. Invented content (`CONTENT-*`)

**CONTENT-01 — One scenario, three bed counts.** `DECISION_LOG #27` and
`HOW_IT_WORKS.md` intro: Command Pod has **8 beds**. `GAME_DESIGN_DECISIONS.md §Starting
scenario`: "Command Pod (**8 beds**, 2 docking ports…)". `P0-0 K04/K05`: the CommandPod
prefab is authored with **8 beds**. `DAY_BY_DAY_PLAN.md §"Locked on the spot"`:
"1 Command Pod (**beds 6**)". Day 21's observable: "8 colonists, **6 beds** → two rest
badly". Four places say 8; two say 6 — and the difference *is* the design decision: at 8
beds there is no housing pressure on day one and the Habitat has no job; at 6 beds,
Day 21's observable depends on a shortage that Day 22's scenario then has to preserve. Class · **D** (the
number) but the *inconsistency* is a **verified defect** in the packet. LRM: Day 2 (K05
authors the prefab) — i.e. **before** it is resolved anywhere. Replacement: fix the source
of truth (`HabitationComponent.capacity` on the prefab — see `CONTENT-02`) and let every
document quote it instead of restating it. While resolving: pick 6, because Day 21's
observable needs housing pressure to exist and 8 beds makes that observable false. That is
derivation from the schedule's own observable, not preference — and it is the only one of
these numbers that can be derived today.

**CONTENT-02 — Bed capacity owned by a list of presentation sockets.**
`P0-D §Housing`: *"`HabitationComponent.capacity` = `ModuleSockets.beds.Count`"*; also
`P0-0 K04` ("beds.Count matches HabitationComponent if present"). Class · **C/D**. Today
capacity is an authored `int` on the Runtime component (`HabitationComponent.capacity = 8`,
`People/HabitationComponent.cs:8`) and nothing enforces it (`CountResidents` merely counts,
`PopulationManager.cs:70-81`). The plan inverts the dependency: the sim's **capacity**
would be owned by a transform list whose only role is animating bodies, in the
Presentation-side convention. That is rule 4 upside down, and it means an artist deleting a
bunk silently changes the economy. Missing information: whether bed count is a number you
tune or a consequence of prefab art — a real question, but answerable *by looking* on the
day. LRM: Day 21 (or Day 2, if `ModuleSockets` is authored first — which is why it matters
now). Replacement: `HabitationComponent.capacity` stays authoritative (it exists,
serialized, rule-11-clean); `ModuleSockets.beds` *displays* up to capacity; disagreement
logs an authoring error — which is K04's own good instinct, applied in the correct
direction.

**CONTENT-03 — The `BuildingDefinition` entry list: Corridor, Habitat, Farm, Water
Processor, Dock Port Module.**
`P0-C §Content`; Day 14. Class · **D**. This is the slice's entire build menu, chosen
before one building has been placed, and it is a *content scope* decision wearing a data
list. Derive the minimum from the schedule's own observables: Day 16's observable requires
a Habitat (a site that requests Regolith, gets deliveries, and completes); Day 17's
requires a Corridor (staff stop flying, then fly again); Day 21's requires a Habitat.
**Farm, Water Processor and Dock Port Module have no observable in the slice that requires
them to be player-buildable** — "Dock Port Module" is even scoped "later" in the same
sentence that lists it. Missing information: what a second Farm *does* strategically (it
is a statement about expansion, and this game's expansion statement is corridors and
planes). LRM: Day 14. Replacement: start with **two** entries (Corridor, Habitat) —
derived, not preferred — and add a third only when an observable needs it. Also delete
`category` and `unlocked` (`ENT-07`).

**CONTENT-04 — The `Builder` role's numbers and `ConstructionRate`'s curve
(0 / 0.6 / 1.0 / 1.3, cap 3, exertion 1.2).**
`P0-C §Content`. Class · **A** for the *assets existing* (the construction site is staffed
by a role; `StaffingRoleDefinition` exists, `exertion` is a real field feeding fatigue),
**D** for every number. Evidence that these are analogies, not derivations: they mirror the
shipped Farm curve's shape (`FarmOperator.asset` — `0.65` at one worker). Missing
information: how long a building should take to feel like a project. LRM: Day 14, on the
day, using the Farm role as a *template* and saying so. Replacement: author the assets,
clone the Farm role's numbers as the starting point **and note in the ticket that they are
clones**, then leave them until one building has been watched. A number with a stated
parentage is honest; a number with no parentage reads as a measurement.

**CONTENT-05 — Ice sized to run out "around day 5–8".**
`ROADMAP.md` Phase 1; `EXPLORATION_AND_LONG_RANGE.md §8` and §"The arc this serves";
DECISION_LOG #32 (`GAME_DESIGN_DECISIONS §Placement and the world`: "sized to run out
around day 5–8"). Class · **D**, and a *narrative* claim (the mid-game is a designed
crisis) enforced by an authored quantity in a Phase 0 scenario for a Phase 1 payoff.
Missing information: the actual consumption rate at the new clock and the actual mining
throughput — neither exists. LRM: after Day 19 (measurement) — not Day 22. Replacement:
**blank the number and measure it.** Day 19's run gains one line: *"log Ice remaining at
hour 72; divide the starting amount by the burn rate; that number of days is the ice
size."* This is *less* work than guessing, it cannot be wrong, and it turns "day 5–8" from
a design assertion into arithmetic. Note also that "day 5–8" is stated with a range, which
is better than a point — but a range over an unmeasured rate is still a guess.

**CONTENT-06 — Water Processor automated (DECISION_LOG #27, `HOW_IT_WORKS §11`,
`GAME_DESIGN_DECISIONS §Starting scenario`).**
Class · **C**, verdict **keep-with-trigger**, and credit Fable for flagging it for veto
(#27). His reasoning is sound *and* narrow: it "demonstrates the shuttle-served trade-off
through freight alone without spending one of eight colonists on it." But the decision's
*real* content is larger than a scenario tweak: it declares **automation a first-class
content option**, which is load-bearing much later (`EXPLORATION §8`: `operatingRole ==
null` means *automated*, for probes). And it removes the player's choice to staff the
processor from the slice's central demonstration. Missing information: whether the
walk-vs-fly trade reads better through freight or through people. LRM: the scenario day —
and the test is one field. Replacement: keep it as the default *and* write the veto as a
one-line experiment on the day ("run it both ways; a staffed processor adds a third commute
and costs a colonist"). Do not let it be locked; do let it be tried. This is the plan's own
best pattern (`P0-S README`: "tune for look, then report them") applied to a scenario.

### 5. Invented preferences (`PREF-*`)

**PREF-01 — "Locked on the spot (needed to plan; veto early)".**
`DAY_BY_DAY_PLAN.md`, immediately after the dependency-flaw table: placement domain, one
construction material, starting scenario. Class · **D** as a *category*, and it is worth
naming the mechanism precisely because Fable did it in the open: **a decision becomes
"locked" when the planning document would otherwise stall.** The heading's own
justification is the finding — these three are locked "to plan," not to build. This is the
prompt's thesis ("*locked* and *on the spot* in the same sentence is the disease") stated
by the plan against itself. LRM: today. Replacement: the plan may lock **nothing**; it may
only defer with a trigger. Two exceptions, both facts rather than choices: (a) existing
code, (b) your own stated words. Rename the section `## Needed to keep writing — three
blanks to veto or accept`, and give each one a trigger instead of a lock. If a decision
genuinely cannot be deferred, it is class B and it needs a *revisit trigger* — which is a
different and much cheaper thing than "locked." Fable uses the revisit-trigger pattern well
elsewhere (`P0-S §3` "revisit when the first ship blocks"); the fix is to apply it here.

**PREF-02 — The "hybrid control" model.** `GAME_DESIGN_DECISIONS.md §Control model`;
DECISION_LOG #6 explicitly says "User choice." Class · **A**. KEEP. No finding. Listing it
to make the audit honest: the audit is not against decisions, it is against unearned ones,
and this one is yours. Its *algorithm* is `ENT-09`.

**PREF-03 — UI Toolkit over uGUI.** `DECISION_LOG #17`; `P0-B README` ("UI Toolkit only
(`UIDocument`, UXML, USS); no uGUI, no IMGUI at runtime"); `ROADMAP §A`. Class · **B/C**,
and a genuine blind spot rather than an over-specification. The reasoning ("Unity 6 runtime
UI; lock to avoid mixing") is fine; the cost of being wrong is a rewrite of every panel;
and the decision is testable in **an hour** — one `UIDocument`, one button, one world-space
raycast through HDRP, one 4 Hz data-driven rebuild, one profiler look. Instead the plan
spends **Day 8** building a shell on the assumption. Missing information: whether UI
Toolkit renders over HDRP at runtime in this project without surprises (the FACTCHECK
notes UI Toolkit *modules are present*, which is not the same as *working over HDRP*).
LRM: before Day 8 — realistically **Day 3** as a spike. Replacement: a one-hour spike
ticket in the Day 1–5 window: *"`UIDocument` over HDRP: does a panel render, can a click
reach a ship's collider, does a 4 Hz rebuild allocate?"* Report the answer; then Day 8 is
either a shell or an uGUI shell. This is the cleanest instance in the audit of "we don't
know yet" being treated as known.

**PREF-04 — Sim-owned 6DOF Newtonian integrator, no PhysX.** `DECISION_LOG #13`
(flagged for veto); `P0-S §2`; `GAME_DESIGN_DECISIONS §Flight and docking`. Class · **B**,
arguably **A**: the reasoning (deterministic, pause-safe, speed-safe, serializable,
testable) is *forced by existing code* — the project's pause/disable/toggle semantics are
load-bearing and PhysX is neither deterministic nor pause-safe. KEEP. Its *numbers* are
`CONST-09`.

**PREF-05 — "Loading/unloading take game-time."** `DECISION_LOG #15`; `P0-S §4`. Class ·
**B** for the mechanism, **D** for the values (`CONST-08`). One thing to protect, and the
plan states it well: *"Inventory mutations still happen atomically at the end of the phase,
so conservation rules are unchanged."* That is a real invariant, correctly located. KEEP.

**PREF-06 — "Dropped tests as a gate."** `HOW_IT_WORKS §14`; `ARCHITECTURE_CONSTITUTION.md`
rule 12; `DECISION_LOG #3`; `STATE_OF_THE_PROJECT` ("the Unity Test Runner has never
produced a result file"). Class · **A** (your call, and an honest one). Not a
de-specification finding — an **unpriced work item**, which is why it is in this audit at
all. Every P0-A and P0-S ticket mandates 5–7 EditMode test files; `T00_CHARACTERIZE`
requires a captured baseline; `T08` requires an `ACCEPTANCE_REPORT.md` with grep evidence.
So the 26 days carry on the order of 40 never-executed test files, written for a runner
that has never run here, by agents whose work is graded on compilation. The plan should
either run them or stop writing them; writing them and calling them "design artifacts" is
the one place in the packet where a decision and its cost are separated. Replacement:
tests are written **only** where the ticket's own reasoning is genuinely uncertain. The
flight integrator's conservation check is a real design artifact (you cannot see momentum
conservation by looking). `RouteResolverTests` case 6 ("`LogisticsManager.Instance == null`
and no walk path") is a straight read of a switch statement and belongs in the ticket text.
One clause in every ticket: *"write a test only if you cannot verify the claim by playing;
say which and why."*

### 6. Sequencing lock-in (`SEQ-*`)

**SEQ-01 — The collapse map.** For each high-leverage C/D decision, which downstream days
consume it, and what goes blank if it changes. This is the deliverable the prompt asks for
in §5 last paragraph.

| Decision | Days that consume it | Blast radius |
|---|---|---|
| `ENT-12` placement rule set | 14 (`footprint`, `maxCorridorLength`), 15, 16 (EVA link "nearest module on the same plane"), 17 (corridor from two nodes), 22 (`sitePlanes[]`, `planeId`, `corridors[] {moduleA,nodeA,moduleB,nodeB}`), 24 (scatter clearance) + `EXPLORATION §8` | **6 days + a Phase 1 document** |
| `ENT-01` needs personal-vs-aggregate | 11 (two need bars), 20, 21 (housing ↔ needs), 23 ("population/needs summary"), 25 (game over "read why") | **5 days** |
| `ENT-06` scenario schema | 22, plus the *shapes* of 14, 15, 16, 17, 18, 20, 21 | **1 day + 7 couplings** |
| Ledger shape (`ENT-10`, `UI-02`) | 9, 10, 11, 23, 25 | 5 days |
| `ENT-09` allocator | 18, plus observables in 20, 21, 23, 25 | 1 day + 4 observables |
| `P0-0 K04` socket set (`ENT-11`) | 5, 12, 13, 15, 16, 22 | 6 days |
| `CONST-13` clock interval | 1, 5, 12, 13, 19, 24, 26 | everything with a "reads well" claim |
| `ENT-05` deposit selection | 14, and Phase 1 discovery (`EXPLORATION §8`) | 1 day + a seam promise |

Reading the map: the four days to specify **least** are Day 2 (sockets), Day 9 (ledgers),
Day 15 (placement) and Day 22 (scenario), because each is load-bearing for five or more
later days. Depth on those four days is exactly where the plan spends it.

**SEQ-02 — Day 11's UI precedes the needs decision by nine days.** `UI-03` / `ENT-01`.
Class · **D** as sequencing. The colonist panel's two need bars are drawn on Day 11 and
filled on Day 20, and in between they are an unexamined constraint on the model. LRM:
Day 11. Replacement: no bars until the answer exists.

**SEQ-03 — Day 22's bootstrap mirrors six systems' shapes.** Fable found this himself
(`F13`) and fixed the *ordering* (moved bootstrap after corridor/prefab paths exist).
He did not fix the *coupling*: the schema is written *as* a copy of six shapes, so when
any shape moves the schema moves. Class · **D** as coupling. LRM: Day 22. Replacement:
`ENT-06`'s derived-asset proposal, which removes the coupling rather than rescheduling it.

**SEQ-04 — The plan tunes twice because survival lands after balance.** Day 19 is
"Integration and economy tuning" and its observable is *"72h with no permanent shortage and
at least one completed building."* Days 20–23 then add needs, death, housing, the report,
and Day 26's playtest tunes again. So the first tuning pass runs against a colony **without
its survival system**, and the second pass has one day. Class · **D** as sequencing, and
it is a real waste rather than a stylistic one. LRM: Day 19. Replacement: separate
*measuring* from *deciding*. Day 19's observable becomes **data collection** — *"72h:
log food-per-colonist, Ice burn rate, blocked-time p95 per subject, blocked maximum, and
the hour each first shortage occurs"* — and every tuning decision moves to the day the
survival system exists. This is strictly cheaper (Day 19's tuning work is deferred, not
duplicated), it feeds `CONST-04`, `CONST-05`, `CONTENT-05`, and it means the second tuning
pass sees a colony with stakes.

**SEQ-05 — Days 20/21/23 name the allocator in their observables, so deferring it is not
free.** `ENT-09`. Class · **B** (the cost is real and small). Replacement: rewrite the
three observables to use manual assignment ("assign a replacement by hand and watch the
vacancy close"). Three sentences; then the allocator is genuinely optional on Day 18.

### 7. Authoritative framing (`AUTH-*`)

**AUTH-01 — `01_LOCKED_DESIGN.md`: "This design is fixed. Tickets implement it; they do
not reinterpret it."**
Every packet (`P0-A`, `P0-S`, `P0-B`, plus `minigame readiness`'s precedent). Class · **D**
as *framing*, and the harm is demonstrable **inside the packet**: `P0-S §3` fixes nine
voyage phases, forbids abort during `Approach`, and gives `Blocked` a retained movement
lease. A ticket that discovers it needs an `Aborting` phase (because the first blocked
ship cannot be recovered without one) has no legal move except "stop and report." So the
document that exists to prevent agent drift instead prevents the *discovery* the agent was
hired to make. Also note what "fixed" is being applied to: `captureAngleDegrees`,
`substepSeconds`, `maxSubstepsPerTick`, `holdingSlots` semantics, and eight invented
constants (`CONST-09`). Missing information: none — the value of this document is
**naming** (so six agents agree what a "voyage" is called), and naming survives
everything. LRM: today. Replacement: rename to `01_WORKING_DESIGN.md`, keep the names and
the API sketches, and change the header rule to: *"Names and seams here are settled for
this packet. Behavior is not. A ticket may change behavior if it says what it changed and
why in its report."* One sentence, and the packet stops punishing its own agents.

**AUTH-02 — `ARCHITECTURE_CONSTITUTION.md`'s "12 binding rules."**
Class · **A** for rules 1–9 (they are consequences of the existing code and the readiness
packet — see Part F for the verification) and **D** for the framing of rules 10 and 12.
Rule 10 (file ceiling) is a style convention; rule 12 (acceptance + tests) is a process
policy. Both are worth having and neither is architecture, yet the header says *"Any ticket
that violates a rule must say so and justify it"* — which turns a style preference into a
gate an agent must argue with. Compounded by `CORR-02` (two divergent copies) and `CORR-03`
("binding" under a banner that says "proposals"). LRM: today. Replacement: rules 1–9 keep
the word binding; 10–12 move to `## Working conventions`; delete the `ROADMAP` copy.

**AUTH-03 — `DECISION_LOG.md`: 33 entries in the declarative, of which most are C/D.**
The document's title is the finding. Its *structure*, though, contains the fix: four
entries are marked "Flagged for veto" (#13, #14, #22, #27) and there is a closing section
"Vetoes requested from the owner (not yet answered)". Class · **D** as framing, **credit**
as instinct. Missing information: none. LRM: today. Replacement: **invert it.** The log
becomes *questions with triggers* (Part D), and "decisions" are reserved for the things
that are actually settled: facts about code, and your stated words. The distinction is one
column — status: `decided` / `proposed — trigger pending` / `open` — and applying it is
mechanically checkable.

**AUTH-04 — `GAME_DESIGN_DECISIONS.md §Open` has a Recommendation column, and the plan
resolves open questions in tickets.**
GAPS reproduces it with "Recommendation" and "Decide by" columns; `CORR-04` shows Day 14
resolving one of them; `BLANK-02` shows three scope commitments sitting in the table.
Class · **D**. A table titled *Open* (with a header saying "do not resolve inside a
ticket") whose rows carry answers is a *decision table* wearing an *open* label, and its
rows are then quoted as settled. LRM: today. Replacement: split the table. Anything with
an answer you would defend is class B (move it, and tag a revisit trigger). Anything else
is a backlog question. A row that says "Recommendation: <answer>" and "Decide by: Day 12"
is neither.

**AUTH-05 — The veto list is 26 days long.** `DECISION_LOG` "Vetoes requested…" (#13,
#14, #22, #27) is short and good. The prompt's own guardrail ("the human vetoes; agents
propose") plus `DAY_BY_DAY_PLAN`'s "Locked on the spot (needed to plan; veto early)"
presumes a human reading the whole schedule and picking fights. The realistic failure is
not disagreement — it is that nobody reads it, and the document becomes authority **by
default**. Class · **D**. Missing information: a human's attention budget. LRM: today.
Replacement: the veto list is **one page**, items only, and every item is a *question*
("should a commute cost 15 game-minutes?") rather than a paragraph of design. If a veto
request needs a paragraph, it is not ready to be asked. Part D is written to that budget.

**AUTH-06 — `GLOSSARY.md` defines ~45 terms, most of them for types that do not exist.**
Class · **D** as framing (*a definition implies existence*). The FACTCHECK already
enumerated the non-existent ones (its D4), and `START_HERE.md` lists the packet statuses.
So the project *knows* which types exist and then writes them all into one flat glossary
where `InventoryComponent` and `ResourceConduitComponent` sit at the same altitude.
Missing information: none. LRM: today. Replacement: two sections — **Exists today** (in
`Assets/Scripts`) and **Proposed** — and each proposed entry links to its backlog question.
Mechanical, cheap, and it makes the plan's own status legible at a glance, which is
exactly what this audit is arguing for. Note it also makes `CORR-02` impossible to repeat.

### 8. Deletion of the blank (`BLANK-*`)

**BLANK-01 — `HOW_IT_WORKS.md §2` narrates a day in a colony that has never been played —
in enough detail to anchor expectations.**
Class · **D** as *framing*, and — importantly — **not** as content: this is the single
most *useful* document in the packet for a human reader, and it is how the owner will
decide whether he wants the game. Deleting it would be a loss. The problem is tense and
register: it reads as *observed* ("At one port — it's free — berth granted"),
systematically including the guesses (`CONST-01`, `CONST-08`, `ENT-01`, `ENT-05`,
`CONTENT-06`), so a reader cannot tell a verified fact from a scene-setting sentence.
Missing information: which of its ~60 assertions are predictions. LRM: today. Replacement
— cheap and constructive: **rewrite each paragraph as a prediction with an owner day.**
"§00:05 — the Farm wakes up. *Predicts:* Staffing publishes 0.65 at one tech.
*Falsified by:* Day 3. *If false:* the curve is wrong, not the sentence." That converts the
walkthrough into a **test plan** — the highest-value artifact in the packet becomes
runnable — and it keeps every word that makes it readable. This is the audit's
recommendation for the whole packet in miniature: keep the story, label the verbs.

**BLANK-02 — `GAPS_AND_OPEN_QUESTIONS.md` answers questions it opens.**
`## Open questions` carries a **Recommendation** column; the `Design` section recommends
the module kit source and colonist identity depth; the "Interior visibility"
recommendation is a full art-direction call ("Cutaway roofs on selected/hovered module +
always-visible corridor tubes with windows") with a deadline ("Decide by Day 12").
Class · **D**. The recommendation for interior visibility would determine the entire module
art kit, the Facilities presenter's camera needs, and how death is *noticed* — decided in a
table cell. LRM: **Day 7** for the interior question (the day the camera exists), not
Day 12 as scheduled; the rest when reached. Replacement: delete the Recommendation column;
keep the question and a **trigger** — for interiors: *"when you can fly the camera, look at
the colony and decide whether you can tell it's alive."* One clause added to Day 5's
observable in Part C.

**BLANK-03 — `ROADMAP.md` Phases 1–3 read as plans.** Conduits, prefab towing, mobile
habitats, morale, research, trade, 8–12 resources in 3 chains, 200 colonists at 4×, Steam
integration, modding. Class · **C** — far enough out not to bind by date — with two
exceptions that *already bind Phase 0*: `SitePlane`-for-multiple-sites (`ENT-12`/`ENT-08`,
which pushed an abstraction into Day 15) and "keep `FreightSupply` vehicle-agnostic"
(a correct instruction whose rationale is a Phase 2 conduit). LRM: when the Phase 0
playable exists. Replacement: keep the roadmap — it is genuinely useful for the owner to
see the shape of the game — but mark each Phase 1+ item as **horizon, not input**, and
move the short list of Phase-0 seams it justifies into **one page** (the `EXPLORATION §8`
format) that is the only part of it Phase 0 reads.

**BLANK-04 — `EXPLORATION_AND_LONG_RANGE.md §8` is the best-designed section in the
packet, and two of its items are still premature.**
Credit first: "Seams Phase 0 must keep open (actionable now)" is **the format this audit
recommends for every C/D finding** — it names the seam, names what to avoid, and states
why (each item says the second act then "costs no rewrite"). It is also honest ("Not a
locked design. These are the nods…"). Two items break its own rule: `SitePlane` with
`planeId` (`ENT-08`), and "starting deposit sizes are authored so local ice depletes around
day 5–8 at the starting population" (`CONTENT-05`) — an authored *number* inside a section
about *seams*. Class · **D** for those two items. LRM: Day 15 and Day 19 respectively.
Replacement: item 2 becomes "implement placement against a plane value with an origin and
normal, so a second plane is possible" (no id, no cross-plane corridor rules); item last
becomes "log the ice burn rate on Day 19; the ice size is arithmetic." Everything else in
§8 survives unchanged — including item 5 ("Do not add a vehicle assumption to the
demand/supply match") and item 6 (`operatingRole == null` means automated), which are
exemplary.

**BLANK-05 — The plan contains no record of what it *refused* to decide.**
The packet has a decision log, a glossary, a gaps doc, and an open-questions table — and
in all four, refusals are indistinguishable from answers, because everything is written in
the same declarative register. Class · **D** as an *omission* (the prompt's §8 question 2
cannot be answered from the documents). LRM: today. Replacement: Part D. The backlog is
the missing artifact — not as a new process, but as the *inverse* of `DECISION_LOG.md`:
same 33 entries, split into `decided` / `proposed — trigger pending` / `open`, with the
third group expanded to everything this audit found.

---

## Part B — The dependency collapse, as a table the plan can act on

Already delivered as `SEQ-01`; repeated here in the order the prompt asks for it, because
the *ranking* is the useful part:

**Days whose changes propagate furthest** (specify these least): 2 (sockets), 9 (ledgers),
11 (UI shapes), 15 (placement), 22 (scenario).
**Days that are cheap to defer** (defer these freely): 14 (content numbers), 18b
(allocator), 20 (needs/death), 21 (housing), 23 (report), 24–25 (environment, session),
27–28 (buffer).
**Days that are load-bearing and already minimal**: 1 (enablers — every item is forced by
F1/F2/F11), 3–4 (the staffing split, which is a *move* not a redesign), 16–17 (construction
reusing logistics and staffing — genuinely elegant: a site is an inventory plus a role).

**The single highest-leverage change in the audit**, if only one is taken: `ENT-06` —
derive the scenario asset from the live scene instead of hand-authoring a 15-field schema.
It removes one day's design work, removes seven couplings, and deletes the plan's largest
invented entity. Second highest: `ENT-09` — split Day 18 into "targets are data" (commit)
and "the allocator" (trigger-gated). Third: `SEQ-04` — make Day 19 measure instead of tune,
which makes `CONST-04`, `CONST-05` and `CONTENT-05` self-answering.

---

## Part C — Rewritten plan skeleton (proposed replacement for `DAY_BY_DAY_PLAN.md`)

### The rule

> **Horizon may sketch shapes. Depth may not.** At any moment, exactly one day is
> *committed* (dispatch tomorrow as-is), the next four exist as *working sketches*, and
> everything beyond is a **question with a trigger condition** (Part D) — never a design.
> A committed day states four things: the work, the observable, **what it expects to
> learn**, and which decision points it feeds. A day that learns nothing is not a day.

This keeps everything Fable's structure gets right — one day's work decided in advance, an
observable every day, incremental building on what yesterday taught — and removes the
depth. Days 1–5 below are **Fable's Days 1–6, minus the class-D material**; I have not
redesigned anything, and if you prefer his ordering the change is logistical, not
conceptual.

### Committed window (Days 1–5)

**Day 1 — Enablers only. No design.**
- *Work:* `Presentation` + `UI` asmdefs (empty); the additive scene split
  (`Managers`/`Base`/`Environment`/`UI`/`Bootstrap`) + `EditorBuildSettings`; `ModuleSockets`
  with **`meshRoot`, `dockPorts`, `dockApproaches` only** (the three Day 5 needs);
  `gameHoursPerRealSecond` exposed in the Inspector and **changeable during Play Mode**.
- *Press Play and see:* the identical colony running from `Bootstrap`, at a pace you can
  change while it runs.
- *Expects to learn:* (i) **is 1 game-hour / 60 real-seconds readable?** The plan asserts
  "at 1 h/s nothing is watchable" as a premise; this is the first moment it can be
  falsified, and it is one inspector field. (ii) whether the scene split removes agent
  collision — **not** confirmable today (one author); it is confirmable on Day 3 when two
  agents land in different scenes. Say so rather than claiming it.
- *Feeds:* `CONST-13` (the clock); `ENT-11` (socket set).

**Day 2 — Prefabs, and only the sockets that have a consumer.**
- *Work:* convert the five modules to prefabs under `Assets/Prefabs/`; primitives under
  `meshRoot`; **one** dock port + approach transform per station (not two — see
  `CONTENT-01`); no components on the ports yet.
- *Press Play and see:* identical behavior; socket gizmos in the Scene view.
- *Expects to learn:* which socket kinds the *first* consumer actually needs. The plan
  authors eight categories on Day 2 for consumers that land on Days 5, 13, 15 and 16; the
  honest number today is three.
- *Feeds:* `ENT-11`; `CONTENT-01` (bed count, which K05 authors — resolve it here).

**Day 3 — Split staffing (part 1) ∥ flight math ∥ the UI Toolkit spike.**
- *Work:* `T00` characterize + `T01` formatter + `T02` EmploymentRegistry;
  `ShipFlightProfile` + `FlightIntegrator` + `FlightGuidance` (superset of Fable's `S02`);
  and the **one-hour spike** from `PREF-03`: does a `UIDocument` render over HDRP at
  runtime, can a click reach a ship's collider, does a 4 Hz data-driven rebuild allocate?
- *Press Play and see:* identical staffing behavior; a 2,000 m hop's game-second count
  with the placeholder profile; a throwaway panel with one button.
- *Expects to learn:* (i) the real responsibility boundaries inside `StaffingManager`,
  from the `T00` artifact rather than from a draft's guess; (ii) **does a 2,000 m hop read
  as a journey or a teleport** — the plan currently *asserts* "60–120 game-seconds" and
  then authors `traversalHours`, `loadGameHoursPerUnit` and `substepSeconds` around the
  assertion; (iii) whether the UI stack holds, before Day 8 spends a day on it.
- *Feeds:* `PREF-03` (UI stack), `CONST-07`, `CONST-08`, `CONST-09`.

**Day 4 — Split staffing (part 2) ∥ the voyage phase machine ∥ the walk seam.**
- *Work:* `T03` + `T04`; `ShipVoyageComponent` with **Docked → Undocking → Cruise →
  Approach → FinalDocking** (no `Holding`, no queue, no `TryAbortToNearestBerth`);
  `TransitLinkComponent` + `TransitGraph` + `RouteResolver` with one link.
- *Press Play and see:* a scratch-scene voyage pushes back, flips, burns, brakes, captures;
  staffing behaves identically; `TransitGraph.Links.Count == 1` and drops to 0 when the
  component is disabled.
- *Expects to learn:* (i) **whether a docking queue is needed at all** — you cannot know
  that until a second ship visits one port, and building `Queued`/`Holding`/priority-or-FIFO
  first guarantees you will rationalize the queue you already built; (ii) whether
  resolving routes by traversal-hours (Dijkstra) beats "links are links" at the scale of a
  base — the answer at 1 link is "obviously not," but the *shape* of the resolver is what
  `ENT-08` hangs on, so learning it cheaply matters.
- *Feeds:* `ENT-08` (resolver complexity), `ENT-13` (the queue), `ENT-10` (ledger shape).

**Day 5 — Hands and eyes: the first day a human touches something new.**
- *Work:* migrate the three ship callers to voyages + timed load/unload authored at
  **0**; the `Corridor CommandPod-Farm` link in `Base.unity`; `ColonistView` walking legs;
  **camera + selection moved forward from Day 7** (disjoint directories, and it is what
  makes Days 6–12 reviewable at all).
- *Press Play and see:* freight completes via a real voyage; farm staff show `Walking` with
  zero passenger contracts while water staff fly; a ship holds at a busy port; you can fly
  the camera and click a ship. **And:** you can look at the colony and tell whether it is
  alive.
- *Expects to learn:* (i) **does cargo moving instantly read as wrong?** Author
  `loadGameHours = 0` and let the observable fail — that failure is the number you wanted
  (`CONST-08`). (ii) **does the walk/ship split read as a strategic choice?** This is the
  plan's entire genre claim and it is first falsifiable here — not on Day 6, not on
  Day 17. (iii) the interior-visibility question (`BLANK-02`), answered by looking rather
  than by a table cell on Day 12. (iv) whether sockets are in the right places once ships
  actually move (Day 2's claim, finally testable).
- *Feeds:* `CONST-07`, `CONST-08`, `BLANK-02`, `ENT-13`.

### Horizon (question + trigger; no depth)

Everything from Fable's Days 6–28 survives as a *shape with a trigger*. This is not a
schedule; the next five days are written when these five have taught their lessons.

| Shape (what it is, one line) | Start specifying it when… | Stays blank until then |
|---|---|---|
| **Queues, holding, port modules** — finite berths, holding slots, priority-or-FIFO | a second ship wants one port and you can see what should happen | the grant policy, `holdingSlots` count and overlap (`ENT-13`), `Denied` reasons |
| **Corridor → staff switch** — build a corridor, staff stop flying | you have watched a walk and a shuttle and have an opinion about the trade | `traversalHours` (`CONST-07`), corridor length limit (`ENT-12`) |
| **Construction content** — a material, a Builder role, `BuildingDefinition` | you have picked up a ghost | the build menu's contents (`CONTENT-03`), every rate (`CONTENT-04`) |
| **Placement** — plane, ghost, valid/invalid, confirm | the hour the ghost is draggable | the rule set (`ENT-12`), the snap angle (`CONST-03`) |
| **Sites** — inventory + import policy + Builders + progress | one site exists to look at | `evaRange`, the EVA-link shape, whether sites get ports |
| **Corridors & demolish** — permanent links; unwind reservations and staff | you have built one corridor | refund fraction, what happens to staff (`ENT-04`'s invariant) |
| **Staffing targets** — target per role×shift, priority, pin, HR shows the gap | you have run a shift change by hand **and** hand-assigned twice | the allocator's release policy, throttle, hysteresis (`ENT-09`) |
| **The allocator** — fills targets via `Assign()` | *"you hand-assigned a shift change twice and wished you hadn't"* | all four invented behaviors; what happens to a worker whose target moves |
| **Needs** — nutrition, hydration, warnings | a shortage happens and you want to know *who* is hungry, not *that* the colony is | personal-vs-aggregate (`ENT-01`), thresholds (`CONST-01`), the aggregation semantic (`ENT-02`) |
| **Death** — `Kill(colonist, reason)` unwinds every holder | the first colonist dies | the *order* (invariant + adversarial test instead: `ENT-04`); what "died" means to the player |
| **Housing** — beds, homeless, home assignment | beds become the binding constraint | `0.5` (`CONST-02`), whether capacity lives on the component or the sockets (`CONTENT-02`), 6-vs-8 (`CONTENT-01`) |
| **Scenario & bootstrap** — new game from data | all the above shapes have settled | the whole schema (`ENT-06`); prefer deriving the asset from the live scene |
| **Report & alerts** — what went wrong, in words | you have lost a run | the tables (`UI-02`), the four thresholds (`CONST-04`) |
| **Environment** — generator, dust, scatter, lighting | the base reads as alive but bare | art bible particulars; `ENVIRONMENT_ASSETS.md` is already correctly scoped |
| **Session frame** — menu, game over, hotkeys | the game can be lost | every hotkey and layout detail (`CONST-12`) |
| **Playtest & balance** — measure, then decide | Day 19's measurement exists | every number it would set (`CONST-04/05/06`, `CONTENT-05`) |

### What the rewritten plan deliberately never does

It never says "locked." It never states a number it cannot derive from code, from your
words, or from a measurement it schedules. It never specifies a system before the day that
system's first consumer exists. It never spends a line on a choice that is reversible in
one inspector field. And every committed day still ends with **"Press Play and see"** —
blankness is never an excuse for vagueness about *today*.

---

## Part D — `DECISION_BACKLOG.md` (proposed)

Every C/D decision extracted from the packets, the day plan, and the root documents.
Format: the question · trigger that fires it · who decides (**the owner, always**) ·
current blank · where it currently lives (so demotion is traceable).
Nothing here is scheduled. Nothing here is a design. **This is the missing artifact
(`BLANK-05`).**

### Proposed — trigger pending (was "locked")

| # | Question | Trigger | Current blank | Was in |
|---|---|---|---|---|
| B-01 | Do needs belong to *people* or to *places*? | A shortage happens and you want to know *who* is hungry | Everything about `ColonistNeedsComponent` | `P0-D §Needs`, `ROADMAP §D` |
| B-02 | How does one impaired worker degrade a facility — average, worst, or fractional headcount? | A colonist is impaired and you notice (or don't notice) the facility slowing | `WorkEffectiveness` and the effect-curve semantics | `P0-D §Needs`, `StaffingEffectRule` |
| B-03 | What should running out of food *feel* like — how much warning, how loud? | You have watched the meter empty and felt nothing | All death thresholds | `P0-D §Needs`, `GAME_DESIGN_DECISIONS §People` |
| B-04 | Is a bed a soft tax, a hard block, or a death clock? | You have watched a colonist sleep somewhere you did not intend | Homeless restfulness | `P0-D §Housing` |
| B-05 | Should a commute cost 15 game-minutes, or should walking be cheaper than that? | You have watched a full corridor walk and had an opinion | `traversalHours` | `P0-A T07`, locked design |
| B-06 | Does cargo loading take visible time, or does it read fine instantly? | Day 5's observable fails — cargo moves and nothing reads | `loadGameHoursPerUnit/Passenger` | `P0-S §4` |
| B-07 | Should rotation snap at 15°, or should it be continuous? | You are dragging a ghost and it feels wrong | The snap angle | `DAY_BY_DAY_PLAN` "Locked on the spot" |
| B-08 | Is the clock right at 1 game-hour / 60 real seconds? | Day 1: press Play and try to read the movement | The interval itself | `P0-S §0`, K03, #14 |
| B-09 | Should the player choose which deposit the miner works, or should it choose? | There are two rocks and you want one | Registry, range, ranking rule | Day 14, #24, `EXPLORATION §8` |
| B-10 | What does a construction site's EVA link look like? | The first site is placed | `evaRange`, link shape, whether sites get ports | Day 16, F7 |
| B-11 | Should a queue ever move a queued ship, and what happens when holding slots run out? | A second ship queues at one port | Grant policy, slot count, overlap behavior | `P0-S §1`, K05 |
| B-12 | Should the allocator ever *move* a worker who is already employed? | You hand-assigned a shift change twice and wished you hadn't | Release policy, throttle, hysteresis, fill order | `P0-C §Staffing targets`, `ROADMAP §C` |
| B-13 | How does the player think about assignment — by person, by job, or by number? | You have run one full shift change by hand | The HR interaction model; keep the reason strings | `P0-B §6`, U06 |
| B-14 | Which sockets do the modules actually need? | Each new consumer lands (Day 5 ports, Day 13 slots, Day 15 nodes) | Six of the eight categories | `P0-0 K04`, #21 |
| B-15 | What belongs in the *first* build menu? | You have placed a ghost and wanted a second thing | Farm / Water Processor / Dock Port Module entries | `P0-C §Content`, Day 14 |
| B-16 | How precisely must a ship dock — and is that a property of the ship or the port? | A docking feels clumsy or feels trivial | `captureDistance/Speed/Angle` | `P0-S §1` |
| B-17 | Should ports be player-buildable, and what does a port module cost? | You queue at a station and want another berth | The Dock Port Module as a buildable | `P0-C §Content` |
| B-18 | When local ice runs out — how many days is the mid-game crisis? | Day 19's 72h run gives the burn rate; then it's arithmetic | The authored deposit amount | #32, `EXPLORATION §8`, `ROADMAP P1` |
| B-19 | Is the starting crew 3/2/3, and how many beds does the Pod have? | Day 19's run shows whether 2 farmers can feed 8 | Crew counts **and** 6-vs-8 beds | #27, K05, Day 21, `HOW_IT_WORKS` |
| B-20 | How much food should the colony start with? | Day 19's run shows the real consumption rate | `startingStock[]` | Day 22, #27 |
| B-21 | Which numbers should the Builder role and `ConstructionRate` start from? | One building has been watched end-to-end | Cap 3, exertion 1.2, the 0/0.6/1.0/1.3 curve | `P0-C §Content` |
| B-22 | What are the four alert thresholds? | Day 19 gives you p50/p95 for each metric | 12h / 2h / 1h / 8h | Day 23, `P0-B §7` |
| B-23 | What should the colony report contain, and for whom? | You have lost a run and could not tell why | The tables, the sparkline, the five sections | Day 23, `P0-B §7` |
| B-24 | Does the player need to see inside modules? | Day 5: look at the colony and decide whether it reads as alive | Roofs, windows, cutaways, camera needs | `GAPS` (answered!) |
| B-25 | Does UI Toolkit hold over HDRP at runtime here? | Day 3 spike | The UI stack (cost of being wrong: every panel) | `P0-B README`, #17 |
| B-26 | Which way does one sick worker move the *authored curves* in `Assets/GameData`? | Same trigger as B-02 | Whether `CurveAt(int)` becomes `CurveAt(float)` | `StaffingRoleDefinition`, `ENT-02` |
| B-27 | Should any facility be automated in the slice? | Try it both ways on the scenario day: a staffed processor costs a colonist and adds a commute | Whether the Water Processor has a role | #27, `HOW_IT_WORKS §11` |

### Open — no answer proposed, and none needed yet

- B-28 Interior visibility (B-24 resolves the *question*; the *kit* awaits it).
- B-29 Module kit source: generated kit-bash vs. purchased (the plan's recommendation is
  a decision it should not make — `BLANK-02`).
- B-30 Colonist identity depth in the slice (names, colour, a trait) — a scope commitment
  currently sitting in an open-questions table (`PREF`-class).
- B-31 Immigration cadence and its gate — Phase 1; the table's "surplus food *and* free
  bed" is a Phase-1 answer in a Phase-0 document.
- B-32 Corridor pressurization/damage — Phase 2; note the plan is right that the "closed
  link = blocker" seam already supports it (that is a *good* seam claim, verified).
- B-33 Resource chains beyond one construction material, and how many resources Phase 1
  should have.
- B-34 Difficulty presets and scenario numbers — after the first playable, by definition.
- B-35 Multiple sites / inter-asteroid shipping — Phase 2; note it is *already* costing
  Phase 0 a `planeId` field (`ENT-08`). Remove the field; keep the question.

### Demotion list (mechanical, so this is checkable)

| Currently says | Becomes |
|---|---|
| `GAME_DESIGN_DECISIONS.md §Locked` heading | `§Proposed — binding once you say so` |
| `GAME_DESIGN_DECISIONS.md §Open` → Recommendation column | deleted; rows become B-24…B-35 |
| `GAME_DESIGN_DECISIONS.md §People` "hard threshold → death" | B-03 |
| `GAME_DESIGN_DECISIONS.md §Placement and the world` (plane, 15°, one material, ice timing) | B-07, B-15, B-18, B-25-class |
| `GAME_DESIGN_DECISIONS.md §Starting scenario` (bed count, crew split, 48h food) | B-19, B-20 |
| `ARCHITECTURE_CONSTITUTION.md` rules 10 & 12 | `## Working conventions` |
| `ARCHITECTURE_CONSTITUTION.md` rules 1–9 | **stay binding** (Part F) |
| `ROADMAP.md §1` rule list | delete; link to the canonical file (`CORR-02`) |
| each packet's `01_LOCKED_DESIGN.md` | `01_WORKING_DESIGN.md`, names settled / behavior mutable |
| `DECISION_LOG.md` #22, #23, #27 | B-07/B-15, B-15, B-19/B-27 — with #13/#14/#22/#27 already flagged; extend the flag to everything C/D |
| `DAY_BY_DAY_PLAN.md §"Locked on the spot"` | deleted; the three items become B-07/B-15, B-19/B-20, B-09 |
| `HOW_IT_WORKS.md §2` | each paragraph gains `Predicts / Falsified by / If false` (`BLANK-01`) |
| `GLOSSARY.md` | two sections: Exists today / Proposed (`AUTH-06`) |

---

## Part E — De-specification pass on the five worst offenders

Before/after, so the change is auditable. Each "after" specifies only seams and
first-questions, and each keeps the day runnable.

### E-1. `P0-D §Needs` — the worst offender in the packet

**Before** (verbatim, condensed): *"`ColonistNeedsComponent` on the colonist prefab:
`nutrition`, `hydration` 0..1, serialized. `PopulationResourceConsumer` reports per-colonist
satisfaction each hour (`Fed(colonist, resource, fraction)`); unmet fraction drains the need
at an authored rate (`needDrainPerHour`), satisfaction restores toward 1. Fatigue recovery
multiplier = `lerp(0.4, 1.0, min(...))` … colonist work multiplier = same curve, exposed as
`ColonistStatusComponent.WorkEffectiveness`; `StaffingComponent` multiplies its per-worker
contribution by it … `hydration == 0` for 24h or `nutrition == 0` for 72h → `PopulationManager.Kill`.
Colonist panel shows the two bars; alerts at < 0.3."*

**After:**

> **Day 20 — what running out of food does (seam only).**
> The colony already knows it is short: `PopulationConsumptionEntry.ShortageActive` is true
> when a draw fails (`People/PopulationResourceConsumer.cs:32-45`). Today that fact reaches
> nobody visible.
> - *Work:* one `ReadinessHistory("consumption.shortage")` transition when `ShortageActive`
>   flips, with the inventory and resource as the subject. No new component, no new field,
>   no new rate.
> - *Press Play and see:* disable the Farm; within a shift, the Command Pod's Food draw
>   fails and a shortage appears in the alert list and the history dump.
> - *Expects to learn:* whether "the colony is short" is enough of a fact to make you act —
>   **this is the only question that decides whether needs are personal** (B-01).
>   If it is enough, the personal model was never needed and you have saved a rewrite.
> - *Feeds:* B-01, B-02, B-03, and Day 21's housing question.
> **Not today:** `ColonistNeedsComponent`, per-colonist satisfaction, `WorkEffectiveness`,
> any drain rate, any death threshold, any need bar. All five are in the backlog with
> triggers. The colonist panel shows no need row yet (B-13 removed the reserved bars).
> **The seam protected:** the alert list and `ReadinessHistory` — so the day a needs
> system exists, it is a *writer* to a channel that already works, not a new channel.

### E-2. `Day 22 §Scenario` — the largest invented entity

**Before:** `ScenarioDefinition` with `sitePlanes[]`, `modules[] {BuildingDefinition, pose,
planeId, initialStock[], staffingTargets[]}`, `corridors[] {moduleA, nodeA, moduleB,
nodeB}`, `ships[] {prefab, dockModule, portIndex, flightProfile}`, `deposits[]`, `colonists[]`,
`startHour`, `clockDefaults`; `ScenarioBootstrap` builds it through the construction
completion path; hand-authored `Base.unity` content deleted.

**After:**

> **Day 22 — new game from the colony you built (mechanism only).**
> - *Work:* **two commands, no schema.** (1) `CaptureScenario(path)` on the construction
>   manager: walks `Base.unity`, and writes a `ScenarioDefinition` asset from what is
>   actually there, *through the same object graph the bootstrap reads*. (2) `New Game`:
>   clears `Base.unity` and replays that asset through `ConstructionManager`'s completion
>   path.
> - *Press Play and see:* build a colony by hand (which you have been doing for three
>   weeks), capture it, press New Game, get the same colony back.
> - *Expects to learn:* whether "the scene is the scenario" holds — i.e. whether anything
>   in the current colony is state you cannot re-create from data. That is the only real
>   question, and it is answered by *round-tripping*, not by designing a schema.
> - *Feeds:* B-19, B-20 — the captured asset is where those numbers come from, and Day 19's
>   measurement is what fills them.
> **Not today:** the field list. It is whatever the writer emits, and it stops being a
> design decision. The fifteen invented fields, and the seven couplings in `SEQ-03`, go
> away; `planeId`/`portIndex`/`nodeA`/`nodeB` were mirrors of other systems' guesses and
> should not exist (`ENT-06`, `ENT-08`).
> **The seam protected:** DECISION_LOG #28's own reason for the bootstrap — scenario and
> construction cannot diverge — is *better* served by deriving than by hand-authoring.
> `ScenarioBootstrap` survives as the consumer.

### E-3. `Day 15 §Placement` — the deepest over-specification

**Before:** `SitePlane` (origin, normal, `planeId`); `PlacementRules.Evaluate(def, pose,
plane) → {Valid, Overlaps, OffPlane, CorridorTooLong, CorridorNotStraight, CorridorIntersects,
NoNodeInReach, Locked}`; modules snap to the nearest free `attachmentNode` pair within a snap
radius else free placement at 15° steps; corridors are straight same-plane segments ≤ max
length with no footprint intersection; "never hard-code y = 0."

**After:**

> **Day 15 — you can put a thing somewhere.**
> - *Work:* a ghost you can move and rotate; `PlacementResult { Valid, Overlaps }` — where
>   `Overlaps` means the ghost's bounds intersect any placed module's bounds; confirm creates
>   a **site** (empty until Day 16). Rotation is continuous (B-07). The plane is a value with
>   an origin and a normal, defaulted, with no id and no cross-plane rules.
> - *Press Play and see:* pick the Habitat, drag a ghost, watch it turn red when it
>   intersects the Pod, clear, confirm, and get a site marker.
> - *Expects to learn:* **whether free placement or node-snapping is the interaction** —
>   the plan currently says "snap to the nearest free node pair **else free placement**",
>   which is both, which is neither. This is a mouse question and it is answered in a minute
>   of dragging; it cannot be answered on paper.
> - *Feeds:* B-15, B-10, and Day 16/17.
> **Not today:** `OffPlane`, `CorridorTooLong`, `CorridorNotStraight`, `CorridorIntersects`,
> `NoNodeInReach`, `Locked`, `planeId`, the snap angle, max corridor length. Corridors are
> not placed on Day 15 at all — Day 17's observable only needs *a* corridor to exist, and
> the cheapest version is two clicks on two modules.
> **The seam protected:** `PlacementRules` is a pure function of (definition, pose,
> existing footprints) so every rule added later lands here, and `TryPlace` already returns
> a result enum. The seam is the *shape* of the call, not the contents of the enum.

### E-4. `Day 23 §Colony Report and alerts` — five sections nobody asked for

**Before:** five sections (population/needs; resource table with 24h in/out/net,
hours-until-empty and a sparkline of 24 buckets; blocked-time table; duty failures as
sentences; transport table with average wait and holding per port) plus four alert rules.

**After:**

> **Day 23 — why did this go wrong, in one screen.**
> - *Work:* one screen, one section: the last handful of `colonist.death`, `*.blocked`,
>   `route.unreachable` and `consumption.shortage` events as plain sentences, most recent
>   first, each clickable to select its subject. Alert rules read thresholds from a
>   **data table that ships empty**, with a `[ContextMenu] Dump last 24h` printing p50/p95/max
>   per metric.
> - *Press Play and see:* leave the Water Processor without a pilot, run 48h, and read
>   *"Water Processor: no eligible pilot for Shuttle shift B — 6.5h blocked"* without
>   opening the Inspector. *(This is Fable's own observable, verbatim, and it does not need
>   a single table.)*
> - *Expects to learn:* what you actually want to know after a run goes wrong — and whether
>   the numbers matter at all, or only the sentences.
> - *Feeds:* B-22, B-23, Day 25's game-over screen.
> **Not today:** the resource table, hours-until-empty, the sparkline, the transport table.
> All four are backlog, and one of them (`HoursUntilEmpty` in the first hours of a run,
> when the flow rate is zero) is a live division-by-zero that would render as "∞ until
> empty" — which reads as *safe*. If it ships later, "unknown" must be expressible.
> **The seam protected:** read models supply every number (ledger → panel, rule 18), so a
> table added later is a view, not new computation.

### E-5. `P0-C §Staffing targets and allocator` — the most player-visible guess

**Before:** `StaffingTarget`, `workPriority` 1–10, `SetTarget`/`SetPriority`/`SetPinned`,
`WorkforceAllocator` at tick 90, throttled hourly, four-step fill/release descending by
priority excluding higher-priority workplaces, 4-game-hour hysteresis, HR v2 with target ±,
priority slider, pin toggle, allocator switch, and an "allocator changed this" badge.

**After:**

> **Day 18 — the colony can see a gap.**
> - *Work:* `StaffingTarget { role, shiftId, target }` on `StaffingComponent`;
>   `workPriority`; `SetTarget(role, shift, n) → TargetResult`; a pin flag; and HR columns
>   showing **assigned / target** with the gap highlighted. **No autonomous behavior.**
> - *Press Play and see:* set Farm / Farm Operator / Shift B to 2; the screen says 1 of 2;
>   assign someone by hand and watch it close; nothing happens on its own.
> - *Expects to learn:* whether setting a number and waiting is *unsatisfying* — which is
>   the only honest test of whether an allocator is needed, and it cannot be run before the
>   manual loop exists.
> - *Feeds:* B-12, B-13, and Day 20/21/23's observables (rewrite them to "assign by hand",
>   per `SEQ-05`).
> **Not today:** the allocator. Trigger: *"you hand-assigned a shift change twice and wished
> you hadn't."* Then there are exactly two questions left — may it *move* an employed
> worker, and how does it avoid thrash — and both are answered by watching a live colony,
> not by reading a draft.
> **The seam protected:** the control model is *yours* (DECISION_LOG #6) and it is preserved
> exactly: targets per role per shift, priority, pin, one API, manual mode intact. Tick
> priority 90 stays specified (it must fill before staffing plans a shift — a real ordering
> constraint). What is removed is the *algorithm*.

---

## Part F — Protect list

Nothing below is a compromise; it is what the de-specification must not touch. Every item
is either verified in code or is your own stated words.

### A1 — Class A survives because code forces it (verified this session)

| # | Invariant | Evidence |
|---|---|---|
| A1 | Inventory is the sole quantity authority, with discrete normalization at every boundary | `Economy/InventoryComponent.cs` — `onHand/reserved/capacity`, `ResourceQuantityRules` on every mutation; `public static IReadOnlyList<InventoryComponent> Inventories` (`:64`) |
| A2 | The converter never counts workers | `Production/ResourceConverterComponent.cs:109-120` reads only `performance.IsOperational` / `GetMultiplier(effect)` |
| A3 | Facility influences flow through `IFacilityPerformanceProvider` channels; consumers never learn why | `People/FacilityPerformanceComponent.cs:11-68`; staffing publishes (`People/StaffingComponent.cs:145-177`) |
| A4 | Employment is explicit and mutated only via `Assign/Unassign`, returning a result enum | `StaffingManager.ValidateAssignment` / `AssignmentResult` |
| A5 | The pilot lease is distinct from employment | `ShipComponent.ResponsiblePilot` vs `EmploymentAssignment`; `TryBoardResponsiblePilot` also requires `pilot.currentLocation == shipLocation` |
| A6 | One authority for a ship's pose, one writer for ship phases | `ShipComponent.MovementPhase`, `TryClaimMovement`; the plan's `ShipVoyageComponent` is the *right* consolidation of three hand-rolled callers |
| A7 | Menus/policies publish; contracts commit; reservation only after a vehicle wins | `Logistics/LogisticsManager.cs`, `Logistics/ContractManager.cs:284-288` |
| A8 | Extraction stays outside freight arbitration | `Extraction/ExtractionMissionController.cs` owns its own state machine, no `ContractManager` |
| A9 | Registry discipline: `FindObjectsByType` only at startup | `ShipComponent.Ships`, `InventoryComponent.Inventories`, `LogisticsManager.DiscoverTransportVehicles()` in `Awake/OnEnable` |

**Also verified as forced** (Fable's premises that are facts, so his derived work stands):
`InventoryComponent.OnChanged` exists (`:65`) → Day 9's flow ledger has a real feed (F9 ✔);
`ReadinessHistory.Record` is fire-and-forget (`:58`) → the in-memory event is genuinely
needed (F10 ✔); `LogisticsManager.TransportVehicles` + `TransportVehicleComponent.personnelEnabled`
exist (`TransportVehicleComponent.cs:54`) → `RouteResolver` step 5 is a few lines (P0-A ✔);
`ExtractionMissionController.unloadLocation` exists (`:21`) → P0-S's startup docking
validation list is real ✔; `HabitationComponent.capacity`/`restfulnessMultiplier`/
`CountResidents` exist → the housing seam is real ✔; `StaffingEffectRule` curves and
`FarmOperator.asset`'s `0.65` exist → `CORR-01` ✔.

### A2 — Protect the *seams* the plan gets right

- **The staffing split (P0-A Epic G) is a move, not a redesign**, and its seams are
  correctly drawn (five classes behind an unchanged facade, no events "for later," a
  characterization ticket that *measures* before cutting). This is the packet most likely
  to be under-specified by mistake; do not relax it.
- **Both corridor-walking and shuttle-service as first-class, with no third option**
  (`P0-A §Part 2`). This is your strategic core in your words ("that's part of the game's
  strategy core"), the resolver procedure is explicit and ordered, and "step 5 checks
  *existence* of a personnel vehicle, not availability — a busy shuttle is a queue, not a
  block" is a genuinely subtle and correct call. Protect the *rule*; the cost constant is
  B-05.
- **Publish-then-commit, and "neither → visibly Blocked."** Preserved in the rewrite
  verbatim.
- **Per-waypoint arrival commits** (`P0-A` tick semantics). This is what makes re-routing
  trivial after a corridor closes, and it is the cleanest expression of "arrival means
  arrived" in the packet. Protect the semantics *and* `LegProgress01` as the presentation
  seam.
- **The additive scene split (F1) and the two asmdefs (F2).** Both are forced, both are on
  Day 1, both are honestly scoped. Keep them on Day 1.
- **`ModuleSockets` as a convention** (the concept, not the eight-category list — B-14).
  Five systems needing the same transform names is real; a convention is the right answer.
- **`EXPLORATION_AND_LONG_RANGE.md §8`'s format.** "Seams Phase 0 must keep open
  (actionable now)", each item naming the seam, the trap, and the reason. This is the model
  for every C/D finding in this audit (`BLANK-04`).
- **The flight profile as content with numbers labeled placeholders** (`P0-S README`:
  "tune for look, then report them") and gains derived from physics rather than authored as
  PD constants. This is the single best instance of guessing *correctly* in the packet —
  isolate the guess in a content asset, say it is a guess, and derive everything downstream.
- **The four veto flags in `DECISION_LOG`** (#13, #14, #22, #27) and the "Vetoes requested
  from the owner" section. The instinct is right; `AUTH-05` says only that the list must be
  one page and the items must be questions.
- **The 2026-09-20 banner pass.** Ten documents de-authorized in one pass is the strongest
  single act in this packet. Its only flaw is that the headings underneath still claim
  authority (`CORR-03`) — fix the headings, keep the banners.
- **The daily observable ("Press Play and see…") and the commit-per-ticket rule.** Both
  survive every rewrite above, unchanged.
- **The "daily rhythm" paragraph** (morning dispatch, midday compile + Play, afternoon
  review + commit; "if the observable fails, the next day starts with the fix — never with
  new scope"). That last clause is the best process sentence in the packet. Keep it.

### A3 — Protect a piece of *code* as a design statement

> `ColonistAgent.OnDisable`: *"Disable is a temporary experiment, not destruction.
> Population and employment registries retain this identity so capacity cannot open a false
> slot while the GameObject is inactive."* (`People/ColonistAgent.cs:129-135`)

That comment is the owner's method already living in the codebase: an unexamined case left
deliberately un-collapsed, with the reason written down. The planning packet should read
like it. Where the packet instead *fills* a blank, it is usually because whoever wrote the
line did not consider that "blank with a reason" was an available answer — which is the
entire finding of this audit, stated in a source comment.

### A4 — Protect the ordering fixes

F1 (one scene → additive), F2 (asmdefs before five consumers), F3 (socket convention),
F9/F10 (ledger feeds), F11 (clock), F14 (collider follows the sim root, not the mesh root):
all verified as real dependencies resolved at the right place. F13's *ordering* fix is also
correct; only the coupling remains (`SEQ-03`).

---

## What I could not judge

- **Agent throughput.** 26 days × 1–3 agents is the schedule's load-bearing assumption and
  the one thing nobody can verify from a repository. Every day-level estimate inherits it.
- **The new clock's effect on tuning.** `FACTCHECK` §Judgment calls this a hypothesis; I
  agree, and `B-08` is why I put it on Day 1 rather than in a document.
- **Art, audio, HDRP and asset-pipeline claims** in `ENVIRONMENT_ASSETS.md` (volumetric fog
  parameters, `Texture3D` sizes, LOD targets, the art bible). I verified the tool is
  correctly *shaped* (Editor-side, seeded, re-runnable, no Runtime coupling, dock-approach
  clearance) and did not verify any of its numbers.
- **Whether the 26-day target is right.** I audited the plan's *commitments*, not its
  calendar. Notably, the rewrite removes work (fewer fields, fewer constants, one fewer
  tuning pass, no hand-authored scenario schema) — so it should be shorter, not longer.
- **Two things I verified only partially:** the three `P0-B` ledgers' exact event types
  (`ENT-10` says pick one mechanism, not that either is wrong), and `P0-A`'s line-count
  targets (`CONST-11`).
- **The sibling reviews** in the project root (`readiness_deepseek41flash.md`,
  `readiness_musespark12.md`) audit the *current code* at earlier revisions, not the plan. I
  read one for bearing on `ENT-04` and found its containment-by-transform-parenting finding
  no longer reproduces at this revision (`ColonistAgent.MoveToLocation` no longer reparents —
  `ColonistAgent.cs:145-149`). **Cite that class of finding only at the revision you build
  on.** If those reviews are meant to be synthesized with this one, the synthesis should
  state each review's revision and note that code findings age faster than plan findings.

---

## If you take exactly one thing

**The plan is not too long. It is too sure.** Every document in the packet is written in
one register — declarative — so a fact about shipped code and a guess about a game nobody
has played read identically, and the reader cannot tell which is which without doing what I
just did. The cheapest possible fix is not to delete anything: **add one word to the
register.** Mark every sentence that is a prediction, and mark every sentence that is a
fact. `HOW_IT_WORKS.md` becomes a test plan. `DECISION_LOG.md` becomes a question log.
`01_LOCKED_DESIGN.md` becomes a names-and-seams document. None of that requires deciding
anything new — which is exactly the point, because the alternative is deciding 63 things
now in order to feel finished.
