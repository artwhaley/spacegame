# Adversarial Plan Audit — the Blank Space Audit

**Subject:** `artwhaley/spacegame` @ `75e5784` ("Add Fable's preliminary planning review and packet drafts (tentative)")
**Date:** 2026-09-20
**Question asked:** not "is the plan accurate" but "does it decide things nobody knows enough to decide yet".

**Method:** repo cloned at `75e5784`; all 13 new root documents and all 20 new packet files read in full;
`Assets/Scripts` (45 files, 7,275 lines) read or grepped to separate decisions *forced by code that exists*
from decisions *invented to fill space*. One note on method: `PLANNING_REVIEW_FACTCHECK.md`, referenced by the
audit brief, is **not present in this commit**. Where a mechanical claim mattered to a classification, I verified
it against the source myself and cite the file and line.

This file contains five deliverables. To split it into the repo, cut at the `<!-- FILE: -->` markers.

---

## Part 0 — How to read this

### The verdict in one paragraph

The plan is not wrong and it is not lazy. It is *overconfident about the future in exactly the places where the
future is cheapest to learn*. Its load-bearing structural moves — the scene split, the `StaffingManager` split,
one voyage authority, the walk-else-ship-else-blocked resolver — are forced by code that exists on disk and
should be kept without argument. Underneath those, roughly sixty smaller decisions were made about systems
nobody has compiled, watched, or played. Four of them are flagged for your veto in `DECISION_LOG.md`; the other
fifty-six are not flagged at all, and several are already load-bearing in tickets three weeks downstream. The
repair is not a new plan. It is: shorten the committed window to three days, move every unearned decision into
one visible register with a trigger, and stop using the word "locked" for systems that do not exist.

### Classes

| Class | Meaning | Treatment |
|---|---|---|
| **A — Forced** | A mechanical consequence of existing code or of your stated intent. | Keep. Protect. |
| **B — Needed now** | Genuinely blocks near-term work; no cheap experiment informs it first. | Keep, tag with a revisit trigger. |
| **C — Decidable later** | More information is coming, and it is coming soon. | Blank it. Specify the seam only. |
| **D — App-shaped** | Invented to fill space. Constants, schemas, layouts, balance, UI nobody has used. | Delete from tickets. Move to backlog as an open question. |

Every finding is **VETO-ABLE**, never "wrong". Fable's reasoning is preserved in each entry so your veto is informed.

### The axis Fable's plan is missing, and this audit adds

Not every guess costs the same to reverse. A number in a `[SerializeField]` is free to change while the game
runs. A schema consumed by six downstream tickets costs a week. Sorting findings by *reversal cost* rather than
by *wrongness* tells you where to spend your attention:

| Reversal cost | Examples in this plan | Priority |
|---|---|---|
| **Free** (Inspector field, content asset) | clock rate, fatigue thresholds, farm curve, hotkeys, alert numbers | Low. Note them as dials, not decisions, and stop writing them into prose. |
| **Cheap** (one file, no consumers yet) | `PlacementResult` enum values, allocator hysteresis, tick priority 90 | Low-medium. Fine to guess *inside the ticket that builds it*, never earlier. |
| **Expensive** (a schema or an authority other tickets consume) | `ScenarioDefinition`, the ledger APIs, `ModuleSockets` field list, bed-count authority, per-colonist needs API | **High. This is where the audit's weight sits.** |
| **Irreversible-ish** (a decision that deletes a question) | interior visibility, rationing policy, what death means | **Highest. These get answered by accident.** |

The findings below are ordered by the brief's categories. The ones that matter most are
**F-07, F-08, F-09, F-11, F-19, F-27, F-28**. If you read eight entries, read those.

---

<!-- FILE: AUDIT_FINDINGS.md -->

## Part 1 — `AUDIT_FINDINGS.md`

### Category 1 — Invented constants

---

#### F-01 · Death thresholds: hydration 0 for 24h, nutrition 0 for 72h · **Class D**

**Where:** `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` § Needs (Day 20).

**The tell, and it is a good one:** `DAY_BY_DAY_PLAN.md` Day 20 says *"Death at nutrition 0 for N hours."*
The blank existed. One directory over, the packet filled it with 72 and invented a second threshold (24) for a
need the day plan never mentioned. This is the disease caught in the act: a space stayed blank in one file
until another file painted it.

**Missing information:** what starvation *feels like* at 4×–10× on the retuned clock. Whether a colonist dying
is a rare shock or a regular bookkeeping event. Whether hydration is even a separate need or a second name for
the same pressure. None of this is knowable before a food economy has run unattended for three game-days.

**Last responsible moment:** the day you want to watch a colonist die on purpose. Not before.

**Replacement / the blank that should exist:** `PopulationManager.Kill(colonist, reason) → KillResult` is the
seam and it is fine (see F-11). *Nothing in the slice needs to call it automatically.* Ship the command with
the only caller being a context-menu item on the colonist, so you can kill someone by hand and watch what
strands. The rule that decides when death happens is blank, and its trigger is "you have run the colony for
72 game-hours with the farm disabled and can describe what you saw."

---

#### F-02 · Homeless restfulness `0.5` · **Class D**, and it contradicts Constitution rule 9

**Where:** `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` § Housing (Day 21); `DAY_BY_DAY_PLAN.md` Day 21.

**Verified against code:** `HabitationComponent.restfulnessMultiplier` already exists
(`Assets/Scripts/ColonyPrototype/People/HabitationComponent.cs`), as a per-facility authored float defaulting
to `1f`, with an `EffectiveRestfulnessMultiplier` guard. So the field is real. What is invented is (a) the value
`0.5`, and (b) the idea that a colonist with *no facility* carries a facility's multiplier.

**The deeper problem:** Constitution rule 9 says *"'Disabled' means paused and a visible blocker — never
silently automated, never a silent fallback."* A homeless colonist sleeping at 50% efficiency is precisely a
silent fallback. The plan's own constitution argues that homelessness should surface as a **blocker with a
reason**, which is also more interesting: you would *see* it in the HR screen instead of inferring it from a
slower fatigue curve.

**Missing information:** whether homelessness should be a penalty or a refusal. That is a game-feel question
with two defensible answers and you have not watched either.

**Last responsible moment:** the first time you have more colonists than beds on purpose.

**Replacement:** Day 21 builds bed capacity enforcement and a `Homeless` state that is *visible*. The
consequence of being homeless is blank. `restfulnessMultiplier` stays authored per facility, where it already
lives and already works.

---

#### F-03 · Alert thresholds: food < 12h, colonist blocked > 2h, port holding > 1h, site starved > 8h · **Class D**

**Where:** `DAY_BY_DAY_PLAN.md` Day 23; `tickets/P0-B_Interaction_UI/01_LOCKED_DESIGN.md` §7.

Four numbers that encode "what is worth interrupting the player for", decided before anyone has been
interrupted. Reversal cost is free, so this is a low-priority finding with one exception: **the alert list
determines what the game thinks is important**, and shipping four of them teaches the player what to care
about. That is design, wearing a threshold's clothing.

**Last responsible moment:** after the first unattended 72h run, where you will notice the two things you wished
the game had told you.

**Replacement:** `AlertRules` reads a list of authored rule assets. The list ships **empty**. The first entry is
written the day after the first playtest, by you, in your words.

---

#### F-04 · Farm production curve "0.65 for one tech"; `ConstructionRate` curve `0/0.6/1.0/1.3`; Builder role `min 1, cap 3, exertion 1.2`; Regolith buffer target 60; site policy priority 8 · **Class C (free to reverse)**

**Where:** `HOW_IT_WORKS.md` §2; `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` § Content, § Construction site.

These are content-asset values. Changing them is a two-second Inspector edit, so as *decisions* they are nearly
harmless. The harm is entirely in F-27: `HOW_IT_WORKS.md` narrates `0.65` as a fact about your game in a
document written "for a human, not an agent". Numbers become real by being read.

**Replacement:** keep the curves as placeholder content authored *by the ticket that first needs them*, and
strip every specific number out of prose documents. A curve in an asset is a dial. A curve in a design document
is a claim.

---

#### F-05 · 15° rotation snapping · **Class D** (the snap increment), inside a **Class B** decision (see F-19)

**Where:** `DAY_BY_DAY_PLAN.md` § "Locked on the spot"; `GAME_DESIGN_DECISIONS.md` § Placement;
`tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` § Placement.

15 is the number you use when you have not dragged a module yet. 24 increments around a circle is a feel
decision that takes ninety seconds to evaluate once a ghost exists, and cannot be evaluated at all before.

**Last responsible moment:** the hour after `PlacementGhost` first renders.

**Replacement:** the ghost exposes a `rotationSnapDegrees` field with no authored value defended in any document.
You set it while dragging.

---

#### F-06 · Starting scenario: 8 colonists split 3 Pilots / 2 Farm Techs / 3 Builders, 48h of food, N beds · **Class D**, and the guess has already drifted

**Where:** `GAME_DESIGN_DECISIONS.md` § Starting scenario; `HOW_IT_WORKS.md` §1; `DAY_BY_DAY_PLAN.md`
§ "Locked on the spot"; `tickets/P0-0_Skeleton/00_README_AND_TICKETS.md` K05; `DECISION_LOG.md` #27.

**Evidence that unearned numbers rot, in one commit:**

| Source | Command Pod beds |
|---|---|
| `GAME_DESIGN_DECISIONS.md` § Starting scenario | **8** |
| `HOW_IT_WORKS.md` §1 | **8** |
| `P0-0` K05 (prefab authoring) | **8** |
| `DAY_BY_DAY_PLAN.md` § Locked on the spot | **6** |
| `DAY_BY_DAY_PLAN.md` Day 21 observable ("8 colonists, 6 beds → two rest badly") | **6** |

The Day 21 demo only works at 6. The prefab ticket authors 8. Both are in the same commit, both written by the
same reviewer, three days apart in reading order. Nobody is being careless; the numbers simply have nothing
holding them in place, because no play experience produced them. Three colonist classes at 3/2/3 has the same
property: it is an arithmetic that makes the walkthrough in `HOW_IT_WORKS.md` read well.

**Last responsible moment:** Day 19-equivalent (first economy tuning pass), when the food loop has actually run.

**Replacement:** the starting composition is blank. The scene keeps whatever is in `SpaceSim.unity` today until
the economy has run unattended once. `DECISION_LOG.md` #27 is already flagged for veto — good instinct, but it
is flagged as one decision when it is really six (headcount, class mix, bed count, food buffer, which facility
is automated, which facility is corridor-joined).

---

### Category 2 — Invented entities

---

#### F-07 · `PopulationResourceConsumer` "reports per-colonist satisfaction each hour: `Fed(colonist, resource, fraction)`" · **Class D — the most expensive finding in this audit**

**Where:** `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` § Needs (Day 20).

**Verified against code** (`Assets/Scripts/ColonyPrototype/People/PopulationResourceConsumer.cs`): the component
consumes `residents × amountPerResidentPerGameHour × delta` from **one inventory**, per resource, and records
`requestedLastTick` / `consumedLastTick` / `shortageActive` **per entry, not per person**. It has no reference to
any individual colonist and never has had one. `ResidentCount` comes from
`PopulationManager.CountResidents(location)`, which counts `colonists[i].home == location`.

**What the invented API hides:** to report *per-colonist* satisfaction you must first decide **who eats when
there is not enough**. Equal fractional shares? Workers before sleepers? By shift? By fatigue? Children-and-elders
rules you have not designed? The packet answers this — equal shares, implicitly, by handing every colonist the
same `fraction` — without ever naming the question. That is a whole survival-game mechanic decided inside a
method signature, in a draft packet, for a system scheduled twenty days out.

This is the clearest example in the repo of app-shaped bullshit, precisely because it does not look like a
guess. It looks like plumbing.

**Missing information:** what you want scarcity to *mean*. Whether shortage is collective (the colony is
hungry) or individual (that colonist is hungry). Everything downstream — needs bars, alerts, death, the colony
report's population section — inherits this choice.

**Last responsible moment:** before any needs code is written. This one deserves a conversation with yourself,
not a ticket.

**Replacement / the blank:** the existing aggregate model already tells you everything the slice needs:
`PopulationConsumptionEntry.ShortageActive` and `ConsumedLastTick` are public and already serialized. Build the
first needs-adjacent thing on *colony-level* shortage (the shelves are empty, and you can see it), and leave the
distribution policy blank with the trigger: "decide when you have watched the colony go hungry once and formed
an opinion about whether individuals should differ."

---

#### F-08 · `ScenarioDefinition` field list · **Class D**

**Where:** `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` § Scenario (Day 22);
`DAY_BY_DAY_PLAN.md` Day 22.

Ten fields with four nested shapes (`modules[] {BuildingDefinition, pose, planeId, initialStock[],
staffingTargets[]}`, `corridors[] {moduleA, nodeA, moduleB, nodeB}`, `ships[] {...}`, `colonists[] {...}`),
specified for an asset nobody has authored, consuming three other shapes that do not exist
(`BuildingDefinition`, `SitePlane.planeId`, `StaffingTarget`). Reversal cost is high: every field is a promise
to the construction path, the placement rules and the allocator simultaneously.

**Missing information:** what the starting colony actually is (F-06 is unsettled), and what the construction
completion path actually accepts (it will be written on Day 16 and will differ from the sketch).

**Last responsible moment:** the day the bootstrap is written, which is also the day the answer is free.

**Replacement:** do not design a schema. On the day the bootstrap lands, **write an editor command that
serializes the live `Base.unity` into an asset**, then delete the hand-authored scene content and load it back.
The schema is then *derived from what the scene actually contains* rather than from what a planner imagined it
would contain. This is not a new guess: it is the only approach that makes the packet's own rule — "scenario
and construction cannot diverge" — mechanically true rather than aspirational.

---

#### F-09 · `ModuleSockets` full field list authored on Day 2 · **Class C**, with a **Class D** validation rule embedded

**Where:** `tickets/P0-0_Skeleton/00_README_AND_TICKETS.md` K04, K05; `DAY_BY_DAY_PLAN.md` Day 2.

Eight socket categories (`meshRoot`, `attachmentNodes`, `dockPorts`, `dockApproaches`, `holdingSlots`,
`workstations`, `beds`, `evaSpawn`) authored on Day 2 for consumers that arrive on Days 5, 12, 13, 15, 16 and 21.
Five of the eight have no consumer for at least ten days.

**The embedded class-D rule is the serious part.** K04's `OnValidate` specifies:
*"beds.Count matches HabitationComponent if present"*. That single line **moves the authority for bed capacity
from an authored int to a count of transforms**, on Day 2, three weeks before the housing decision is made, and
it does so by way of a validation error rather than a design statement. `HabitationComponent.capacity` is a
`public int` today with a default of 8. After K04, authoring a module with 8 beds requires placing 8 transforms.
That is a ratification of F-10 and of the interior-visibility question (F-28) disguised as a null check.

**Missing information:** whether beds are physical slots or an abstract count. That is the open question
`GAME_DESIGN_DECISIONS.md` itself lists as *"do not resolve inside a ticket"*.

**Last responsible moment:** per socket, the day its first consumer is written.

**Replacement:** `ModuleSockets` ships on Day 2 with **`meshRoot` only** — that one is forced (see F-30 for why
it is urgent) — and each later ticket adds the list it consumes, in the commit that consumes it. `OnValidate`
asserts only what Day 2 can know: `meshRoot != null`, and no socket is the sim root.

---

#### F-10 · `HabitationComponent.capacity = ModuleSockets.beds.Count`; `SetHome` command; auto-homing on Habitat completion · **Class D**

**Where:** `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` § Housing (Day 21).

Three separate decisions in three sentences. The capacity change re-owns an existing fact (F-09). `SetHome` is a
reasonable command shape given Constitution rule 3, and `ColonistAgent.home` already exists — call that
**Class B**. The third, *"Habitat completion auto-homes the homeless (allocator-style, once per hour,
respecting pins)"*, is **Class D and contradicts the plan's own spine**: `GAME_DESIGN_DECISIONS.md` § People says
*"No auto-vacancy filling, no call-ins, no teleporting"*, and `HOW_IT_WORKS.md` §6 lists no-auto-vacancy-filling
among the things deliberately not changed. An auto-homer is auto-vacancy-filling for beds.

**Replacement:** `SetHome(colonist, anchor) → HomeResult` ships. Who calls it is blank. The default caller in the
slice is you, in the HR screen. The trigger for automating it: "you have manually re-homed people twice and
found it tedious."

---

#### F-11 · `PopulationManager.Kill()`'s eight-step unwind order · **Class B on the command, Class C on the order**

**Where:** `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md` § Death; `DAY_BY_DAY_PLAN.md` F12 and Day 20.

Credit where due: F12's underlying claim is **true and forced**. `PopulationManager` holds a registry list,
`ColonistStatusComponent` holds `activeDuty` + `dutyHistory`, `PassengerCarrierComponent` holds who is aboard,
`ShipComponent` holds `ResponsiblePilot`, `EmploymentAssignment` holds employment. Destroying a GameObject does
strand all five. A single explicit command is the right shape and follows Constitution rule 3.

What is invented is the **exact ordering of eight steps through six subsystems whose teardown semantics have
never been exercised**. You do not derive that order in a planning document. You derive it by killing someone
and reading the null-reference exceptions.

**Replacement:** `Kill()` ships as a command that asks each subsystem to release the colonist through that
subsystem's own API and returns a `KillResult` naming what could not be unwound. The packet's own instinct —
*"any step failing is logged and the rest continue; the result names what could not be unwound"* — is exactly
right; extend it and delete the fixed order. The order is discovered on the first kill, and then it is a fact.

---

#### F-12 · `WorkforceAllocator` internals: tick priority 90, once-per-game-hour throttle, 4-hour hysteresis, LIFO release, priority-preemption of workers from lower-priority workplaces · **Class B on priority, Class D on the rest**

**Where:** `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` § Staffing targets and allocator (Day 18).

**Priority 90 is defensible and I will not mark it otherwise**: the allocator must write employment before the
people layer reconciles it, and people-layer components register at 200 today
(`StaffingComponent.SimulationTickPriority => 200`). Keep it, tag it "revisit when a second pre-people tickable exists".

The rest is a scheduling algorithm for the system that *is* the control model. "Unassign the most recently
assigned non-pinned worker" is a policy with visible consequences (it makes the allocator churn the newest
hire); "4 game-hours of hysteresis" is a feel constant; "exclude colonists whose current workplace has higher
priority" is a preemption rule that will produce emergent behaviour you have no way to predict from prose.

**Also flagged:** P0-C proposes `workPriority` 1–10, noting *"(or the staffing component — decide at lock)"*,
while `StaffingComponent.commutePriority` `[Range(1, 10)]` **already exists** in the code. Two 1–10 priorities on
adjacent concepts is a Constitution rule 4 hazard (one authority per fact). Nobody has decided whether they are
the same number.

**Last responsible moment:** the day the allocator is written, and then only for the minimum that makes it run.

**Replacement:** the allocator ships as: fill under-target roles through `Assign`, release over-target through
`Unassign`, skip pinned, toggleable. Churn control, preemption and release order are blank until you have
watched it thrash. The trigger is literally "watch it thrash".

---

### Category 3 — Invented UI

---

#### F-13 · HR screen v1 and v2 layouts · **Class B on the candidate list, Class D on the layout**

**Where:** `tickets/P0-B_Interaction_UI/01_LOCKED_DESIGN.md` §6; `02_TICKETS.md` U06; `DAY_BY_DAY_PLAN.md`
Days 11, 18.

The **candidate list with per-candidate rejection reasons is Class A**, and it is the best UI idea in the plan:
`StaffingManager.GetAssignmentCandidates` (StaffingManager.cs:321) already returns `AssignmentCandidate` values
carrying an `AssignmentResult`, and `Describe(result)` already renders them as text. A screen that surfaces
what the simulation already knows is not an invention; it is a projection. Protect it.

Everything around it is invented: two-panel split, five named columns, a workplace tree, three filter modes
("eligible only / blocked only / off-shift now"), and an *"allocator changed this" badge shown for one game-hour*.

**Replacement:** U06 specifies one thing — select a workplace/role/shift, see every colonist with their
`AssignmentResult` reason, assign and unassign. Layout is whatever the agent produces first. It is a UXML file;
rewriting it is an afternoon, and you will only know what you want after you have used the wrong one.

---

#### F-14 · Colony report table columns and section list · **Class D**

**Where:** `tickets/P0-B_Interaction_UI/01_LOCKED_DESIGN.md` §7; `DAY_BY_DAY_PLAN.md` Day 23.

Five sections, twenty-plus columns, a sparkline of 24 buckets, and a specified sentence in U07's acceptance
criterion: *"Water Processor — no eligible pilot for Shuttle shift B — 6.5h blocked (last Day 2 15:40)"*.

An acceptance criterion that asserts a **string a human has not yet wanted to read** is the sharpest version of
this whole problem. U02 makes it worse by requiring a test that *"produces the sentence format exactly for a
fixture record"* — a regression test pinning a UI string, written before the UI exists.

**Replacement:** the report's first version answers exactly one question, and you name that question after your
first unattended run. The ledgers (F-15) hold raw material; the report is a view over it and costs a day to
rewrite. Delete the sentence-format test.

---

#### F-15 · Ledger schemas: `In24h`, `Out24h`, `NetPerHour`, `HoursUntilEmpty`, 24 hourly buckets, `BlockedInterval`, `DutySummary`'s eight fields · **Class B on existence, Class D on shape**

**Where:** `tickets/P0-B_Interaction_UI/01_LOCKED_DESIGN.md` §4; `DAY_BY_DAY_PLAN.md` Day 9, F9, F10.

F9 and F10 are **real and verified**: `InventoryComponent.OnChanged` exists
(`InventoryComponent.cs:65`, `event Action<InventoryComponent, ResourceDefinition>`) and nothing aggregates it;
`ReadinessHistory.Record` is a static write-through to JSONL (`ReadinessHistory.cs:58`) with no in-memory
subscriber. Something must sit between them and any UI. Class A on the seam; the `OnRecorded` event is a
five-line addition and deserves its lock.

One detail Fable's design papers over: `OnChanged` carries **no delta**. Every aggregate therefore requires the
ledger to cache previous on-hand values per inventory per resource. The design says "computes delta = onHand(now)
- onHand(last)" in a comment, which is correct, but it means the bucket schema is doing bookkeeping the event
does not provide, and that bookkeeping is the part worth getting right.

What is invented is **which aggregates exist**. `HoursUntilEmpty` presupposes a linear-drain model of a colony
whose consumption is bursty. 24 one-hour buckets presupposes the question is "per hour over a day".

**Replacement:** the flow ledger keeps a raw ring buffer of `(gameHour, inventory, resource, delta)` and exposes
one method: give me the events in a window. Every named aggregate is derived by the caller that wants it, in
the commit that wants it. That is smaller code, it is strictly more general, and it commits to nothing. Same for
`BlockedTimeLedger`: keep intervals, derive summaries later.

---

### Category 4 — Invented content

---

#### F-16 · `Regolith` as the single construction material · **Class D**, and it closes a question the same commit declares open

**Where:** `DAY_BY_DAY_PLAN.md` § "Locked on the spot" and Day 14; `DECISION_LOG.md` #23;
`tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` § Content.

`GAME_DESIGN_DECISIONS.md` § Open lists: *"Resource chains beyond Ice→Water→Food; which construction
material(s) exist first?"* — owning phase **"P0-C design"**, under a heading that says
*"Open decisions are listed so nobody resolves them by accident inside a ticket."*

It was then resolved inside a ticket, in the same commit, and recorded in `DECISION_LOG.md` as settled.

**Replacement:** construction needs *a* material, and you need a buildable thing to learn anything about
building. Ship it as one placeholder `ResourceDefinition` authored in the construction ticket with a name you
have not defended anywhere else, and keep the open question open. The material's identity, and whether it is
mined or fabricated, is decided the first time you want the construction loop to be interesting rather than
present.

---

#### F-17 · `BuildingDefinition` field list and entry list · **Class D**

**Where:** `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` § Content (Day 14); `DAY_BY_DAY_PLAN.md` Day 14.

Eleven fields and five entries (Corridor / Habitat 4 beds / Farm / Water Processor / Dock Port Module) for a
content type nobody has authored. Two details show it is space-filling rather than design:

- **`unlocked = true`** is a field for a tech/research gate that `DAY_BY_DAY_PLAN.md` § "What is deliberately not
  in these 26 days" explicitly forbids. The field exists because building definitions in other games have one.
- **"Dock Port Module (adds a port to an adjacent module — later)"** is an entry with its own design deferred
  inside its own definition.

Also: the entry list decides that you can build a second Farm and a second Water Processor, which is a
*game* decision (is this a base-layout game or a base-expansion game?) made by listing prefabs.

**Replacement:** the placement ticket needs exactly one buildable definition to prove placement works, and the
corridor needs one more because corridors are the strategic core. Two entries. The rest is a backlog question:
"what should the player be able to build, and why that list?"

---

#### F-18 · Extraction picks deposits "by highest uncovered foreground demand at the unload destination" · **Class D**, justified by a phase that does not exist

**Where:** `DAY_BY_DAY_PLAN.md` F6 and Day 14; `DECISION_LOG.md` #24;
`tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` § Content.

**The flaw is real (Class A):** `ExtractionMissionController` has a single `public ResourceDeposit targetDeposit`
(ExtractionMissionController.cs:20) and validates against it throughout. A second resource forces a change.
`ResourceDeposit` has no static registry, so one must be added; Constitution rule 7 makes that shape obvious.

**The resolution is invented.** A demand-ranked selector that reads stock policies at the unload destination
couples extraction to the logistics arbitration layer — and `HOW_IT_WORKS.md` §6 lists *"extraction staying
outside freight arbitration"* among the invariants the plan promises to protect. `DECISION_LOG.md` #24 justifies
the complexity with *"discovered deposits work instantly later"*, citing `EXPLORATION_AND_LONG_RANGE.md` §8 —
a Phase 1 document. A Phase 0 selection algorithm is being shaped by an unbuilt Phase 1 feature.

**Replacement:** the mining ship needs to visit more than one deposit. The minimum that achieves it: a registry,
and *nearest deposit whose resource the ship's collector can collect and whose destination has room*. Whether
selection should be demand-ranked is a backlog question with the trigger "you have watched the mining ship make
a run you thought was stupid."

---

### Category 5 — Invented preferences (choices presented as settled, alternatives never weighed)

---

#### F-19 · Placement domain: node-snapping on a `SitePlane`, free XZ, "never hard-code y = 0" · **Class B on nodes, Class D on the plane abstraction**

**Where:** `DAY_BY_DAY_PLAN.md` § **"Locked on the spot (needed to plan; veto early)"**; `DECISION_LOG.md` #22;
`GAME_DESIGN_DECISIONS.md` § Placement; `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` § Placement (Day 15).

The heading is the disease. "Locked" and "on the spot" in the same sentence, for the question F4 itself calls
the one nobody has an answer to ("What do you place buildings ON? There is no ground.").

**What survives as Class B:** modules connect at **authored attachment nodes**, and a **corridor is a segment
between two nodes**. That is forced by your own stated strategic core (walk vs fly), by `DECISION_LOG.md` #7,
and by the fact that `TransitLinkComponent` needs two endpoints. Keep it.

**What is Class D:**

- **`SitePlane` with `origin`, `normal` and `planeId`, plus the instruction "Never hard-code y = 0".** This is
  premature generality, and it is the expensive kind: it exists to support multiple outposts, a Phase 2 feature.
  You are being asked to pay an abstraction tax in Phase 0 to protect a decision you have not made. The honest
  Phase 0 version is a flat plane, and the honest note is "if a second site happens, this generalizes; that is
  a Phase 2 refactor of one file."
- **`PlacementResult`'s eight enum values** (`Overlaps`, `OffPlane`, `CorridorTooLong`, `CorridorNotStraight`,
  `CorridorIntersects`, `NoNodeInReach`, `Locked`, `Valid`). `Locked` is for the unlock system that does not
  exist (see F-17). The others are guesses about which rejections matter.
- **Free XZ placement alongside node snapping.** Two placement models in one system, before either has been
  dragged.

**Last responsible moment:** the day the ghost renders. Which is also the first day you can feel it.

**Replacement:** the placement ticket builds: a ghost, node snapping, footprint overlap rejection, and a result
enum with whatever cases the code actually produces. Planes, corridors-never-cross-planes, free placement and
the rotation snap are blank.

---

#### F-20 · Water Processor is automated in the starting scenario · **Class C**

**Where:** `GAME_DESIGN_DECISIONS.md` § Starting scenario; `HOW_IT_WORKS.md` §5.11; `DECISION_LOG.md` #27.

Credit: `DECISION_LOG.md` flags this for veto, and the reasoning is stated ("without spending one of eight
colonists on it"). But the alternative is never weighed, and the alternative is interesting: a *staffed*
processor is the thing that makes shuttle-served facilities cost you a person as well as a pilot, which is the
strategic trade-off you said is the core. Automating it removes the sharpest version of your own mechanic from
the first thing anyone sees.

**Replacement:** it is a content toggle (`offeredRoles` empty or not). Ship whichever, decide after the first
24h watch, and write it down nowhere until then.

---

#### F-21 · Hybrid control (targets + allocator + pinning) · **Class A on the model, Class B on "both modes share one API"**

**Where:** `GAME_DESIGN_DECISIONS.md` § Control model; `DECISION_LOG.md` #6.

Recorded as your choice, and the constraint that the allocator writes only through `Assign()`/`Unassign()` is
forced by Constitution rule 3 and by the existing `StaffingManager` API. Nothing to strip. Listed here so the
de-specification pass does not throw it out with F-12.

---

### Category 6 — Sequencing lock-in

---

#### F-22 · Dependency collapse map · **the reason the other findings matter**

Each row: if the decision dies, what stops being true. "Collapses" means the day's specification becomes
unwritable, not that the day's goal disappears.

| If this dies… | …these consume it | Blast radius |
|---|---|---|
| **F-07** per-colonist needs model | Day 20 needs, Day 20 death rule, Day 21 housing consequence, Day 23 population section + food alert, Day 25 game-over condition | **5 days, 3 packets.** The entire Week 4 arc rests on an unexamined rationing assumption. |
| **F-19** placement domain | Day 15 placement, Day 16 site + EVA link, Day 17 corridors/demolish, Day 22 bootstrap (`pose`, `planeId`), Day 23 build menu | **5 days.** Every construction day and the scenario schema. |
| **F-09/F-10** socket + bed authority | Day 2 K04/K05 validation, Day 13 Facilities presenter slots, Day 21 housing, Day 22 modules[] | **4 days, and it starts on Day 2**, which is why it is the cheapest one to fix now. |
| **F-15** ledger schemas | Day 9 ledgers, Day 10 facility/ship panels, Day 11 colonist panel, Day 23 report + alerts | **4 days.** All of Week 2's payoff. |
| **F-08** `ScenarioDefinition` | Day 22, Day 25 New Game path, any later balance iteration | 2 days, but it is the schema everything gets authored into. |
| **F-17** `BuildingDefinition` entries | Day 14 content, Day 15 build menu, Day 16 completion, Day 22 modules[] | 4 days. |

The pattern: **the plan's back half is a cantilever off its front half's guesses.** Days 20–25 are specified in
more detail than Days 3–6, despite being three weeks further from anything anyone has seen. That inversion is
the single strongest argument for the rolling window in Part 2.

---

### Category 7 — Authoritative framing of guesses

---

#### F-23 · The word "locked" applied to systems with zero existing code · **Class D framing, real mechanical harm**

**Where:** `tickets/P0-B_Interaction_UI/01_LOCKED_DESIGN.md`; `GAME_DESIGN_DECISIONS.md` § Locked;
`tickets/P0-A_Foundation/01_LOCKED_DESIGN.md`; `tickets/P0-S_Shuttle_Flight/01_LOCKED_DESIGN.md`.

There is a clean line here, and it is worth adopting as a rule:

> **Lock a refactor. Never lock a first draft.**

`P0-A`'s and `P0-S`'s locked designs mostly describe **reshaping code that exists** — a 1,063-line
`StaffingManager` (verified: 1,063 lines, 52 methods) split behind an unchanged facade, and three components
that each hand-roll fly-and-dock unified into one authority. Locking those is legitimate: the thing being
described is on disk, and the lock protects a known shape from drift.

`P0-B`'s locked design describes a camera, a selection model, four panels, an HR screen and a colony report, for
a project with **zero presentation code, zero UI code, one prefab and one scene**. There is nothing to protect
it from. The lock's only function is to make later change feel like a violation.

**Replacement:** rename `P0-B/01_LOCKED_DESIGN.md` to `00_DRAFT_DESIGN.md` and keep only §4's seam
(`OnRecorded` + a Reporting folder) as locked, because that is the part touching existing Runtime code.

---

#### F-24 · "The 12 binding rules" · **Class A rules 1–11, Class C rule 12's second half**

**Where:** `ARCHITECTURE_CONSTITUTION.md`.

I went looking for app-shaped bullshit here and mostly did not find it. Rules 1–11 describe how the code on disk
already works (`InventoryRackView` is genuinely the view template; `ShipComponent.Ships` is genuinely the
registry pattern; `Assign() → AssignmentResult` genuinely exists; `ISimulationTickable` genuinely gates
simulation). A constitution that *describes* an existing codebase is a map, not a guess. **Protect it.**

Two notes:

- **Rule 12's "one focused EditMode test per ticket as a design artifact"** is a process guess. You have 20
  EditMode test files already and have decided the runner is not a gate (`DECISION_LOG.md` #3). Writing tests
  nobody runs, as artifacts, is a cost with an unmeasured benefit. Class C: revisit after five days of the new
  cadence and see whether you read any of them.
- **Rule 9 is being violated by F-02** (homeless restfulness as a silent multiplier). Worth noting that the
  constitution is strong enough to catch the plan's own errors when it is actually applied.

---

#### F-25 · `DECISION_LOG.md` records 33 decisions and requests 4 vetoes · **Class D framing**

**Where:** `DECISION_LOG.md`.

The log is a genuinely good artifact — it preserves reasoning, which is what makes an informed veto possible.
Its defect is that it does not distinguish **"forced by the code"** from **"chosen on your behalf"**. #8 (split
the 1,063-line file) and #23 (Regolith is the construction material) sit in the same table with the same
authority, and only four entries carry a veto flag.

**Replacement:** add one column, `Forced / Chosen / Guessed`, and re-tag. My reading of the 33: roughly
#1, #3, #4, #7, #8, #9, #10, #11, #16, #18, #19, #20, #21, #30 are Forced-or-Stated; #6, #12, #13, #14, #17, #22,
#25, #26, #28, #33 are Chosen (defensible, worth a veto flag); #23, #24, #27, #29, #31, #32 are Guessed and
belong in the backlog rather than the log. That re-tagging is a twenty-minute job and it converts the log from
an authority into a trail, which is what it should be.

---

#### F-26 · `EXPLORATION_AND_LONG_RANGE.md` reaching back into Phase 0 · **Class D**

**Where:** `DECISION_LOG.md` #24, #31, #32; `tickets/P0-E_Environment/00_README_FIRST.md` E03; Day 14.

A Phase 1 design document is shaping Phase 0 code in at least four places: the extraction selector (F-18), the
reservation of `ShipComponent.operatingRole == null` to mean "automated/uncrewed" for future probes, the
environment scatter tool reading "density from a field hook (constant in v1)", and #32's *"starting ice is
sized to deplete around day 5–8 so 'where do we get water now' is the designed mid-game"* — a balance decision
for an act of the game that does not exist, recorded as a locked decision in a roadmap.

This is the subtlest form of the disease: it does not look like over-specification, it looks like foresight.
But it means a Phase 0 ticket is paying rent for a Phase 1 idea you have not agreed to.

**Replacement:** `P1-X/00_STUB.md` already has the right instinct (*"Do not lock before the Phase 0 playable
exists"*). Extend it: **no Phase 0 ticket may cite a Phase 1 document as justification.** If a Phase 0 shape is
right, it is right for a Phase 0 reason.

---

### Category 8 — Deletion of the blank

---

#### F-27 · `HOW_IT_WORKS.md` narrates a game nobody has played · **Class D for §2 and §5, Class A for §3 and §4**

**Where:** `HOW_IT_WORKS.md`.

This document does two different jobs and should be two documents.

**§3 (the ownership table) and §4 (the layer arrows) are the best artifact in the commit.** Fourteen rows of
"this fact, this owner, everyone else reads it", every one of which I was able to check against code. That is a
map of what exists. Protect it, move it into `ARCHITECTURE.md`, and keep it current.

**§2 is a 1,400-word minute-by-minute walkthrough of a game that has never been run.** "00:05 — the Farm wakes
up… the production-rate curve says 0.65." "Beacon blinking, 'Holding · #1'." "A minute later (game time) he
arrives." None of this has happened. It is written vividly, for a human, in a file titled *How It Works* — and it
will anchor your expectations harder than any ticket, because it is the only document that is enjoyable to read.

One drafting artifact confirms it was composed rather than observed: the narrator interrupts himself mid-scene
to correct a fact he had just invented ("the Mining Ship happens to be unloading ice there? No — ice goes to the
Command Center").

**Replacement:** split into `ARCHITECTURE_OWNERSHIP.md` (§3, §4, §6 — keep, it is real) and
`INTENDED_EXPERIENCE_DRAFT.md` (§1, §2, §5 — keep, but retitled so it reads as a pitch, not a description).
Nothing is deleted. The *tense* changes from "what happens" to "what we're aiming at", and that is the whole fix.

---

#### F-28 · `GAPS_AND_OPEN_QUESTIONS.md` answers its own open questions · **Class D**

**Where:** `GAPS_AND_OPEN_QUESTIONS.md` § "Open questions (copy of GAME_DESIGN_DECISIONS.md § Open, with
recommendations)".

The document whose entire job is to hold blanks has a **Recommendation** column and a **Decide by** column. Six
open questions arrive pre-answered with deadlines:

| Question | Pre-filled answer | Deadline attached |
|---|---|---|
| Interior visibility | "Cutaway roofs on selected/hovered module + always-visible corridor tubes with windows" | Day 12 |
| Module kit source | "Generator kit-bash first; buy later" | Day 13 |
| Colonist identity depth | "Names + colour + one trait line in the slice" | Day 22 |
| Immigration gating | "Surplus food *and* free bed" | Phase 1 |

**Interior visibility is the one that matters**, and it is worth dwelling on because it shows how a blank dies.
`GAME_DESIGN_DECISIONS.md` marks it Open with the note "do not resolve inside a ticket". It drives the entire
module art kit. And it is already answered three times in the same commit without a decision ever being made:
`P0-0` K04 authors `workstations[]` and `beds[]` transforms; `P0-P` P02 places "sleepers in bunks with
animation"; `GAPS` recommends cutaway roofs. By Day 13 the question is settled by accretion, and you never got
asked.

**Replacement:** delete the Recommendation and Decide-by columns. An open question with a recommendation is a
closed question with manners. Move all six into the decision backlog (Part 3) with triggers instead of dates.

---

#### F-29 · The `— **open**` marker that leaked · **Class D, but credit the instinct**

**Where:** `tickets/P0-C_Build_And_Staff/00_DRAFT_DESIGN.md` § Construction site.

The packet contains a genuine blank, correctly marked:

> "otherwise shuttle-served (needs a port? — **open**: sites get a temporary 'EVA port' … **or** builders are
> dropped at the nearest module and EVA from there. Recommend the latter…)"

And then `DAY_BY_DAY_PLAN.md` Day 16 states the recommendation as the plan, with no marker, no alternative and
no note that a question was ever open. The blank survived one file and died crossing into the next.

This is worth celebrating as much as flagging: **the packet knew how to hold a blank.** The mechanism exists.
It just has no protection when content moves between documents. Part 3's backlog is that protection.

---

#### F-30 · Not over-specification, but a real gap the plan created and did not notice · **Class A — fix it on Day 2**

Included because a de-specification pass that only removes things is not an audit.

`LocationAnchor.Awake()` does:

```csharp
if (GetComponent<Collider>() == null)
    gameObject.AddComponent<BoxCollider>();
```

Unity sizes a `BoxCollider` to the mesh on the **same GameObject** when one is present. Every module today has
its primitive geometry on the sim root, so the auto-collider fits the module.

Day 2 (K05) moves every mesh to a `meshRoot` child. After that, `LocationAnchor` adds a default **1×1×1 box at
the origin** to a GameObject with no renderer. Nothing breaks on Day 2 — the observable is "identical
behavior", and it will be. It breaks on **Day 7**, five days later, when `SelectionRaycaster` starts clicking
those colliders and F14's rule ("collider on the sim root, highlight on the mesh root") meets a unit cube.

**Replacement:** Day 2's observable gains one line: *press Play, and every module's collider still wraps its
visible geometry*. That is what an "expects to learn" field is for — it catches the thing the day's own
success criterion is blind to.

---

<!-- FILE: DAY_BY_DAY_PLAN.md (proposed replacement) -->

## Part 2 — Replacement plan skeleton

**What changes:** the horizon stays; the depth goes. You keep committing tomorrow's work in advance and you
keep "Press Play and see". You stop specifying week four.

**The shape:**

- **Committed window: 3 days**, written at working depth. Never more than 3 cards exist at once.
- At the end of each day you write **one new card**, informed by what the day taught. The window rolls.
- Beyond the window: an **ordered shelf of arcs**. Arcs may sketch *shapes*. Arcs may not specify *contents*.
- Every unearned decision lives in `DECISION_BACKLOG.md` (Part 3) with a trigger, not in a ticket.

**Card template** (this is the whole format):

```
### Day N — <title>
- Work:        what agents do. Files named. Forbidden scope named.
- Observable:  Press Play and see ____.
- Expects to learn: ____   ← the day's real output. If it is empty, the day is
                             execution, not development, and should be questioned.
- Feeds:       DB-nn, DB-nn   ← which backlog decisions this day informs
- Blanks held: what this day deliberately does not decide
```

The **Expects to learn** line is the mechanism. A day that learns nothing is a day of typing, and a day whose
learning does not feed a backlog decision is a day whose learning will evaporate.

---

### The committed window

#### Day 1 — Skeleton: assemblies, scenes, clock dial

- **Work:** create `ColonyPrototype.Presentation` and `ColonyPrototype.UI` asmdefs, empty, referencing Runtime;
  Runtime unchanged. Split `SpaceSim.unity` into additive `Managers` / `Base` / `Environment` / `UI` plus a
  `Bootstrap` that loads them; update `EditorBuildSettings`; keep the legacy scene as
  `SpaceSim_legacy.unity`. Singletons live only in `Managers`. Adopt commit-per-ticket.
  Set `SimulationManager.gameHoursPerRealSecond` to a **starting value you will change during the run**.
- **Observable:** the identical colony runs from `Bootstrap` over 24 game-hours — same contract count, same
  duty events for a named pilot as the legacy scene.
- **Expects to learn:** *what pace is actually watchable.* Open the Inspector while it runs and move the dial
  until a shift is boring in the right way. Write down where you stopped. Also: whether anything in the scene
  depended on cross-scene references you did not know about.
- **Feeds:** DB-01 (clock rate), DB-02 (which speeds the HUD should offer).
- **Blanks held:** the speed set. The HUD. Whether the day should be 24 real minutes or 6.

#### Day 2 — Prefabs and one socket

- **Work:** convert the five scene modules/ships and `Person.prefab` into prefabs under
  `Assets/Prefabs/`, geometry moved under a `meshRoot` child, sim components on the root. `Base.unity` holds
  only instances. Create `Core/ModuleSockets.cs` with **one field**: `meshRoot`. `OnValidate` asserts
  `meshRoot != null` and `meshRoot != transform`. Every other socket list is added by the ticket that first
  reads it, in that ticket's commit.
- **Observable:** identical behavior; editing a prefab changes the instance; **and every module's collider
  still wraps its visible geometry.**
- **Expects to learn:** whether `LocationAnchor.Awake`'s auto-`BoxCollider` survives mesh separation (it will
  not — see F-30), and what else in the scene was reading geometry off the sim root.
- **Feeds:** DB-06 (bed authority), DB-07 (socket vocabulary).
- **Blanks held:** attachment nodes, dock ports, approaches, holding slots, workstations, beds, evaSpawn. All
  seven. None of them has a consumer yet.

#### Day 3 — Staffing split, part 1

- **Work:** P0-A **T00, T01, T02** — characterize current staffing, extract `ScheduleReportFormatter`, extract
  `EmploymentRegistry`. Facade unchanged; no public member of `StaffingManager` renamed or removed.
- **Observable:** identical behavior; the T00 characterization report reads true against a 24-hour run.
- **Expects to learn:** whether the five-way seam in `P0-A/01_LOCKED_DESIGN.md` falls where the code actually
  allows, and how many of `StaffingManager`'s 52 methods have no caller outside the class. The answer changes
  what T03/T04 should be.
- **Feeds:** DB-12 (allocator seam), DB-13 (whether `commutePriority` and a work priority are one number).
- **Blanks held:** the allocator. Targets. Anything that writes employment other than `Assign`/`Unassign`.

**Day 4's card is written at the end of Day 3, not now.**

---

### The shelf — arcs in dependency order, shapes only

No dates. Each arc names its **entry trigger**, and an arc cannot be written into cards until its trigger has
fired. Shapes below are one line each on purpose; if a line grows a schema, it has become a ticket and belongs
in the window.

**A1 · Finish the staffing split.** Trigger: T02 landed and the characterization surprised you or did not.
Shape: `CommuteBatcher` + reconcilers; `StaffingManager` under 250 lines behind the same facade.

**A2 · Ships fly for real.** Trigger: clock dialed, prefabs exist. Shape: one voyage authority replacing three
hand-rolled movement sequences; docking ports with a queue; load/unload take game-time. *The flight model, the
port count and the queue policy are decided inside this arc, not before it.* Feeds DB-19.

**A3 · Walk or fly.** Trigger: A1 complete. Shape: authored transit links, a graph, a resolver returning
walk / ship / blocked with no third option, and one corridor in the scene so you can disable it and watch
commutes flip. **This is the arc that proves your strategic core exists.** Do not let anything jump the queue
ahead of it.

**A4 · Look at it.** Trigger: something moves that you catch yourself wanting to watch. Shape: camera,
selection, and one highlight. Feeds DB-06 (you will form an opinion about interiors the first time you try to
watch someone work).

**A5 · Read it without the Inspector.** Trigger: *you have opened the Inspector twice to answer the same
question.* That question is the first panel. Shape: a raw event feed from `OnChanged` and `OnRecorded`, plus
whatever view answers the question you actually asked. Feeds DB-14, DB-15.

**A6 · Build one thing.** Trigger: you have watched a full colony day and can name a layout change you wish you
could make. Shape: ghost, one buildable definition, a site that requests materials through the existing
logistics pipe and needs a worker through the existing staffing API. Feeds DB-08, DB-10, DB-11.

**A7 · Corridors as a build.** Trigger: A6 completes and A3 is proven. Shape: building a corridor changes a
commute from fly to walk; demolishing it changes it back. This is the smallest possible demonstration of the
whole game.

**A8 · Live and die.** Trigger: the colony has run 72 game-hours unattended and you can describe, in your own
words, what running out of food looked like. Shape: a kill command, and whatever needs model your description
implies. Feeds DB-03, DB-04, DB-05.

**A9 · Start from data.** Trigger: you have hand-edited the starting base three times. Shape: serialize the
scene you already have; load it back. Feeds DB-20.

**A10 · Make it look like somewhere.** Trigger: the slice plays end to end. Shape: `ENVIRONMENT_ASSETS.md`
already specifies this well and it touches no Runtime code, which is why it can safely stay specified.

**A11 · Session frame.** Trigger: A8 landed and losing is possible. Shape: enough menu to start and lose.

---

### What the schedule no longer claims

There is no Day 26. There is no "26 working days plus 2 buffer". A slice ships when A7 and A8 are both true,
and you will know the date about four days before it happens, which is the earliest anyone ever honestly does.

The estimate is not lost — it was never real. `DECISION_LOG.md` #5 records "2–3 weeks → 26 working days" as your
choice, and it can stay as an *intention*. What it cannot stay as is a schedule that pre-decides what happens on
the twenty-second of them.

---

<!-- FILE: DECISION_BACKLOG.md -->

## Part 3 — `DECISION_BACKLOG.md`

Every C and D decision, extracted from tickets into one register. **Who decides: you, every row.** Agents
propose; the register is where a proposal waits until it is earned.

Rule for this file: *a row leaves the backlog by being decided, never by being consumed.* If a ticket needs a
row's answer and the trigger has not fired, the ticket is too early.

### Tier 1 — Decisions that will otherwise be made by accident

| ID | Question | Current blank | Trigger | Delete the guess from |
|---|---|---|---|---|
| **DB-03** | When there is not enough food, **who eats?** Is scarcity collective or individual? | Consumption stays aggregate; `ShortageActive` is the only signal | Before any needs code. This one is a conversation, not an experiment. | P0-D § Needs (`Fed(colonist, resource, fraction)`) |
| **DB-06** | Are beds (and workstations) **physical slots or an abstract count**? Equivalently: do we see inside modules? | `HabitationComponent.capacity` stays an authored int | First time you try to watch someone work or sleep (arc A4) | P0-0 K04 `OnValidate`; P0-D § Housing; P0-P P02; GAPS recommendation |
| **DB-04** | What does **death** mean here — a shock, a failure state, or attrition bookkeeping? | `Kill()` exists; nothing calls it automatically | After 72h unattended with the farm disabled | P0-D § Needs thresholds (24h/72h); DAY_BY_DAY Day 20 |
| **DB-08** | Beyond "modules connect at nodes", what is the **placement model**? | Nodes + footprint overlap only | The hour the ghost first renders | DAY_BY_DAY § "Locked on the spot"; P0-C § Placement |
| **DB-11** | **What can the player build**, and why that list? | Two definitions: one module, one corridor | After A7 proves corridor-vs-shuttle is fun | P0-C § Content entry list |

### Tier 2 — Decisions with real consequences, cheap triggers

| ID | Question | Current blank | Trigger | Delete the guess from |
|---|---|---|---|---|
| DB-01 | Clock rate at 1× | The field, dialed live | End of Day 1 | P0-0 K03 (`0.0166667`) |
| DB-02 | Which speeds the player gets | Whatever `SetSpeedMultiplier` clamps to (0.1–10) | When a HUD exists | P0-B §3; DAY_BY_DAY Day 8 |
| DB-05 | Is homelessness a **penalty or a blocker**? | A visible `Homeless` state with no consequence | First time colonists exceed beds | P0-D § Housing (`0.5`) |
| DB-07 | Socket vocabulary | `meshRoot` only | Per socket: its first consumer | P0-0 K04 (8 categories) |
| DB-09 | Do outposts on other planes exist? | Flat. One site. | Phase 2, if ever | `SitePlane` origin/normal/planeId; "never hard-code y = 0" |
| DB-10 | Identity of the construction material | One placeholder resource | When construction should get interesting | DAY_BY_DAY Day 14; DECISION_LOG #23; GAME_DESIGN_DECISIONS § Open (restore) |
| DB-12 | Allocator churn: release order, hysteresis, preemption | Fill under target, release over target, skip pinned | After watching it thrash | P0-C § allocator (LIFO, 4h, priority preemption) |
| DB-13 | Is facility priority **one number or two**? (`commutePriority` already exists) | One number until proven otherwise | When the allocator reads a priority | P0-C § allocator (`workPriority`) |
| DB-14 | Which flow aggregates exist | A raw `(hour, inventory, resource, delta)` buffer | When a panel needs one | P0-B §4 (`In24h`/`Out24h`/`NetPerHour`/`HoursUntilEmpty`) |
| DB-15 | What is the **first question the colony report answers**? | None. The report does not exist. | After your first unattended run | P0-B §7 (five sections, 20+ columns) |
| DB-16 | What is worth interrupting the player for? | Rule list ships empty | After the first playtest | P0-B §7 (12h / 2h / 1h / 8h) |
| DB-17 | Starting composition: headcount, class mix, beds, food buffer | Whatever `SpaceSim.unity` holds today | After the economy runs once | GAME_DESIGN_DECISIONS § Starting scenario; DAY_BY_DAY § Locked on the spot; P0-0 K05 (**and resolve the 6-vs-8 beds contradiction by deleting both**) |
| DB-18 | Is the Water Processor staffed or automated? | Content toggle, undefended | After the first 24h watch | GAME_DESIGN_DECISIONS; HOW_IT_WORKS §5.11 |
| DB-19 | How does extraction choose a deposit? | Nearest collectable with room at the destination | When you watch a run you think is stupid | DAY_BY_DAY Day 14; DECISION_LOG #24 |
| DB-20 | `ScenarioDefinition` schema | Derived by serializing the live scene | The day the bootstrap is written | P0-D § Scenario (10 fields) |
| DB-21 | Should anything auto-assign homes? | No. You do it. | After re-homing manually twice | P0-D § Housing (auto-homing) |

### Tier 3 — Open questions restored to open

| ID | Question | Trigger | Restored from |
|---|---|---|---|
| DB-22 | Module art kit: generated, kit-bashed, or purchased | When placeholder primitives start lying to you | GAPS recommendation ("generator kit-bash first", Day 13) |
| DB-23 | Colonist identity depth: names, portraits, traits | When a death should feel like something | GAPS recommendation ("names + colour + one trait", Day 22) |
| DB-24 | Immigration gating | Phase 1 | GAPS recommendation ("surplus food *and* free bed") |
| DB-25 | Should starting ice deplete on a schedule? | After the water loop has run unattended | DECISION_LOG #32 ("day 5–8", "the designed mid-game") |
| DB-26 | Are per-ticket EditMode tests worth writing if nobody runs them? | After five days of the new cadence | Constitution rule 12, second half |
| DB-27 | Rotation snap increment | While dragging the first ghost | DAY_BY_DAY § Locked on the spot (15°) |
| DB-28 | Every balance constant (farm curve, construction curve, exertion, buffers) | First tuning pass | P0-C § Content; HOW_IT_WORKS §2 (`0.65`) |

### Demotions

The following should be re-marked **"proposed — trigger pending"** rather than locked:

- `GAME_DESIGN_DECISIONS.md` § Locked: **Placement and the world** (all of it), **Starting scenario** (all of it),
  the needs/death sentence under **People**, and the Water Processor's automation.
- `GAME_DESIGN_DECISIONS.md` § Locked survives intact for: Identity, the corridors-vs-shuttles rule, the control
  model, the construction *principle* (site requests materials, no wallet), time and shifts, the fatigue
  numbers already in code, flight sequence, presentation principles.
- `ARCHITECTURE_CONSTITUTION.md`: rules 1–11 stay binding. Rule 12's test clause becomes "proposed".
- `tickets/P0-B_Interaction_UI/01_LOCKED_DESIGN.md` → `00_DRAFT_DESIGN.md`, except §4's `OnRecorded` seam.
- `DECISION_LOG.md`: add a `Forced / Chosen / Guessed` column (see F-25 for a proposed re-tagging of all 33).

---

## Part 4 — De-specification pass

Five rewrites. Each keeps the seam, keeps the observable, and moves the guesswork to a `DB-` row. Nothing below
fills a blank I opened; where I could not derive a seam from existing code, the entry says so.

---

### 4.1 — `P0-D` needs, death and housing

**Before:** a needs component with two 0..1 values, an invented per-colonist satisfaction API, a
`lerp(0.4, 1.0, …)` recovery curve, a work-effectiveness multiplier folded into staffing contributions, two
death thresholds, a bed-capacity authority change, a `0.5` homeless multiplier, and an auto-homer. Seven
decisions, zero of them earned.

**After — three independent tickets, each with one question:**

> **D-a · The kill command.**
> Create `PopulationManager.Kill(colonist, reason) → KillResult`. It asks each subsystem that holds a
> reference to release the colonist through that subsystem's own API — employment via
> `StaffingManager.Unassign`, duty via `ColonistStatusComponent`, passengers via `PassengerCarrierComponent`,
> the pilot lease via `ShipComponent`, the registry via `PopulationManager.Unregister` — and returns a result
> naming every step that could not complete. **No fixed order is specified.** The only caller in this ticket is
> a context-menu item.
> *Observable:* right-click a colonist mid-commute, kill them, and read the result. Their workplace's active
> count drops; nothing throws.
> *Expects to learn:* the correct unwind order, and which subsystem lacks a release path. **That is the
> ticket's actual output.** Write the order down as a fact afterward.
> *Feeds:* DB-04. *Forbidden:* needs, thresholds, any automatic caller.

> **D-b · Colony hunger is visible.**
> `PopulationResourceConsumer` already computes `requestedLastTick`, `consumedLastTick` and `ShortageActive`
> per entry. Surface those. Nothing else.
> *Observable:* disable the farm, run 72 hours, and watch the shortage appear and persist somewhere you can see.
> *Expects to learn:* what running out feels like at the dialed clock — how long it takes, whether it is
> gradual or a cliff, and whether you want it to land on the colony or on individuals.
> *Feeds:* DB-03 — **this ticket's output is the input to the rationing decision, and that decision is yours.**
> *Forbidden:* per-colonist need state, thresholds, death, multipliers.

> **D-c · Beds are finite.**
> Enforce `HabitationComponent.capacity` (the authored int that already exists) when a home is set. Add
> `SetHome(colonist, anchor) → HomeResult` rejecting a full facility. A colonist with no home shows
> **`Homeless`** as a visible state with a reason, per Constitution rule 9.
> *Observable:* set eight homes against six capacity; two are refused, visibly, with a reason.
> *Expects to learn:* whether homelessness wants a consequence at all, and whether you looked for the bed
> *transforms* while you were doing it. If you did, that is DB-06 answering itself.
> *Feeds:* DB-05, DB-06. *Forbidden:* restfulness penalties, auto-homing, deriving capacity from sockets.

---

### 4.2 — Day 22, `ScenarioDefinition`

**Before:** a ten-field schema with four nested shapes, consuming three types that do not exist.

**After:**

> **Ticket: the starting colony becomes data.**
> Write an editor command that walks `Base.unity` and serializes what is there into a `ScenarioAsset`. Write
> the loader that instantiates it into an empty `Base.unity`. **The asset's fields are exactly what the walk
> found** — no field exists because a planner expected it.
> *Observable:* delete the hand-authored contents of `Base.unity`, press Play, and the same colony appears.
> *Expects to learn:* what the starting colony actually consists of, which is not currently written down
> anywhere correctly (see the 6-vs-8 beds contradiction, F-06).
> *Feeds:* DB-17, DB-20.
> *Forbidden:* authoring the asset by hand; adding a field the scene walk did not produce; routing the load
> through a construction path that does not exist yet.

**Note on the packet's own constraint.** P0-D requires the bootstrap to build through `ConstructionManager`
completion paths "so scenario and construction cannot diverge". That is a good instinct with the dependency
backwards: it makes the *scenario* wait for construction. Serializing first means the scenario exists from the
day it is useful, and routing it through construction becomes a later refactor with a clear trigger ("the first
time a hand-built and a player-built module behave differently").

---

### 4.3 — Day 23, colony report and alerts

**Before:** five sections, twenty-plus columns, a 24-bucket sparkline, four alert thresholds, and an acceptance
test asserting an exact sentence.

**After:**

> **Ticket: answer the question you kept asking.**
> Precondition: you have completed one unattended multi-day run and written down, in one sentence, the question
> you most wanted answered while watching it. That sentence is this ticket's spec.
> Build the smallest view that answers it, reading from the raw ledgers (4.5).
> *Observable:* the question is answered on screen, without opening the Inspector.
> *Expects to learn:* the second question.
> *Feeds:* DB-15, DB-16.
> *Forbidden:* sections nobody asked for; thresholds; a test that asserts a display string.

The alert rules ship as an **empty authored list**. The first rule is written the day after the first playtest,
in your words, describing the thing you wished you had been told.

---

### 4.4 — Day 15, placement

**Before:** `SitePlane` (origin + normal + planeId), free XZ placement, 15° rotation snapping, node snapping,
an eight-value result enum including `Locked`, corridors-never-cross-planes, and "never hard-code y = 0".

**After:**

> **Ticket: put a ghost somewhere legal.**
> `PlacementRules.Evaluate(definition, pose) → PlacementResult` where `PlacementResult`'s cases are **only the
> ones the code produces**: valid, footprint overlap, and no node in reach. Modules snap to authored
> `attachmentNodes` (added to `ModuleSockets` by this ticket — its first consumer, per DB-07).
> `ConstructionManager.TryPlace(definition, pose) → PlacementResult`. A ghost renders valid/invalid.
> One buildable definition exists, authored here.
> *Observable:* drag a ghost, watch it snap to a node, watch it go red over an existing module, confirm and see
> a marker where it will go.
> *Expects to learn:* whether node snapping feels like placing a building or like fighting one; whether free
> rotation is wanted at all; whether a flat plane reads as a surface or as an accident.
> *Feeds:* DB-08, DB-09, DB-27.
> *Forbidden:* planes as a type, rotation snapping, corridors, an `unlocked` field, a build menu listing things
> that cannot be built yet.

The corridor rules (straightness, max length, intersection) belong to the corridor ticket, because a corridor is
the only thing they constrain. Splitting them out is not de-specification for its own sake: it means the corridor
arc can reshape them the day it discovers that a straight segment between two nodes looks wrong.

---

### 4.5 — Day 9, the reporting read models

**Before:** three read models with named aggregates (`In24h`, `Out24h`, `NetPerHour`, `HoursUntilEmpty`), a
24-bucket rolling window, interval structs, an eight-field duty summary, and a specified sentence format with a
test pinning it.

**After:**

> **Ticket: remember what happened.**
> Two Runtime additions, both forced (F-15):
> 1. `ReadinessHistory.OnRecorded` — a static event fired inside the existing `Record()`, carrying the fields
>    `Record()` already takes (`gameHour`, `eventType`, `subject`, `detail`, `correlation`).
> 2. `Reporting/EventBuffer` — subscribes `InventoryComponent.OnChanged`, caches per-(inventory, resource)
>    on-hand to compute a delta the event does not carry, and appends `(gameHour, inventory, resource, delta)`
>    to a ring buffer. Same for history transitions. One query: **give me the entries in a window.**
> *Observable:* run 24 hours, inspect the buffer, and find the farm's food production in it.
> *Expects to learn:* how much data an hour actually generates, which determines whether buckets are needed at
> all.
> *Feeds:* DB-14.
> *Forbidden:* named aggregates; bucket schemas; duty summaries; sentence formats; any consumer.

Every aggregate the UI eventually wants is then three lines over the buffer, written by the panel that wants it,
on the day it wants it. This is smaller, strictly more general, and commits to nothing — and it preserves
P0-B's genuinely good rule that **panels never compute aggregates that a ledger should own**, because the
ledger still owns the data. It just stops owning opinions about the data.

---

## Part 5 — The protect list

De-specification is only safe if the structure survives. Everything here is **Class A**, verified against the
code at `75e5784`, and an agent working from this audit must not weaken any of it.

### The codebase invariants (real, load-bearing, hard-won)

| Invariant | Where it lives |
|---|---|
| Inventory is the sole quantity authority | `InventoryComponent`; `InventoryRackView` is a view over it |
| Converters never count workers; they read `IsOperational` / `GetMultiplier` | `ResourceConverterComponent`, `FacilityPerformanceComponent`, `IFacilityPerformanceProvider` |
| Employment is explicit: person + workplace + role + shift, mutated only through `Assign`/`Unassign` returning a result | `EmploymentAssignment`, `StaffingManager` |
| The pilot lease is distinct from employment | `ShipComponent.ResponsiblePilot`, `ShipCrewDutyComponent` |
| Publish-then-commit logistics: demand is cheap, contracts reserve | `ResourceStockPolicyComponent` → `LogisticsManager` → `ContractManager` |
| Extraction stays outside freight arbitration | `ExtractionMissionController` (**protect this from F-18**) |
| Simulation advances only from `SimulationTick`; `Update` is presentation | `SimulationManager`, `ISimulationTickable` |
| Location means *arrived*; transit is a separate fact | `ColonistAgent.currentLocation`, transit components |
| Discrete quantity rules | `ResourceQuantityRules`, `ResourceDefinition.IsDiscrete` |
| No auto-vacancy filling, no call-ins, no teleporting | `StaffingManager` (**protect this from F-10's auto-homer**) |

### The plan's genuinely good structural moves

- **The additive scene split (F1) and the asmdef creation (F2).** One `.unity` file exists and everything edits
  it; one prefab exists. Both flaws are real, both fixes are cheap, both are on Day 1–2 where they belong.
- **The `StaffingManager` split.** 1,063 lines, 52 methods, and both the allocator and the walking system land
  in it. Splitting before adding writers is correct sequencing, and the facade rule protects callers.
- **One voyage authority.** Three components hand-roll fly-and-dock today. Unifying them is a refactor of
  existing code, which is why P0-S's lock is legitimate (F-23).
- **The route resolver: walk if a path exists, ship if not, blocked if neither, and never a third option.** This
  is your stated strategic core, recorded as your own words in `DECISION_LOG.md` #7. It is the most valuable
  sentence in the entire planning commit. Protect it from every future convenience.
- **The candidate list with rejection reasons.** `GetAssignmentCandidates` already returns them and `Describe()`
  already renders them. Surfacing what the sim already knows is the correct definition of a UI.
- **`OnRecorded` and the `OnChanged` seam.** Two small, forced additions.
- **The daily observable.** "If that line isn't true, the day isn't done" is the best process rule in the repo.
  Part 2 strengthens it rather than replacing it.
- **`HOW_IT_WORKS.md` §3 and §4** — the ownership table and the layer arrows. Fourteen rows, all verifiable.
- **`ARCHITECTURE_CONSTITUTION.md` rules 1–11.** A description of how your code already works, not a wish.
- **The "Forbidden" section in every ticket.** This is the one mechanism in the plan that already protects
  blankness, and nobody noticed that is what it does. Extend it: today it forbids *scope*; it should also
  forbid *decisions*. "Forbidden: deciding what beds are." That single change would have prevented six of the
  findings above.
- **The "DRAFT, lock after X" marks on P0-C and P0-D.** The right instinct, applied to two packets out of nine.

---

## Part 6 — Three process changes, and the done check

### 6.1 — Lock a refactor, never lock a first draft

A lock is protection against drift in a known shape. If the shape is not on disk, there is nothing to protect
and the lock only raises the cost of learning. `P0-A` and `P0-S` may keep their locks. `P0-B` may not.

### 6.2 — No Phase 0 ticket may cite a Phase 1 document as justification

Four Phase 0 decisions are currently justified by `EXPLORATION_AND_LONG_RANGE.md` (F-26). If a Phase 0 shape is
right, it is right for a Phase 0 reason, and the Phase 1 document can be updated later by someone who knows more
than either of us does today.

### 6.3 — Every ticket's forbidden list gains a *decisions* clause

Mechanically: each ticket names the `DB-` rows it must not resolve. An agent that finds itself needing one stops
and reports, exactly as it already does for an unlisted file. This is the enforcement mechanism for everything in
Part 3, and it reuses a rule the plan already has.

---

### Definition of done — checked against this audit's output

> **1. What are we building in the next three days, and what will we learn?**
> Part 2's committed window. Three cards, each with an explicit "expects to learn".

> **2. What have we explicitly refused to decide yet, and what triggers each decision?**
> Part 3. Twenty-eight rows, five of them Tier 1, every one with a trigger and a place to delete the existing
> guess from.

> **3. Which decisions survive because existing code forces them?**
> Part 5. Ten invariants and nine structural moves, each traced to a file.

> **4. Does the plan contain zero decisions about systems that do not exist, without a trigger?**
> After Parts 3 and 4 are applied: yes, with one honest exception — `P0-E` (environment) remains fully
> specified. That is deliberate and it is not a violation. It touches no Runtime code, it produces art assets
> whose reversal cost is a regenerate, and `ENVIRONMENT_ASSETS.md` is a tool spec rather than a design. Leaving
> it alone is the audit refusing to de-specify for the sake of it.

---

### One closing note, offered as a finding about the process rather than the plan

Fable's review was asked to produce a plan, and a plan is a document that answers questions. Asked for twenty-six
days, it produced twenty-six days' worth of answers, because that is what the request's shape demanded. The
over-specification is not a flaw in the reviewing model so much as a flaw in the *unit of work requested*.

The durable fix is not this audit. It is asking for three days at a time, and for the question each day is
supposed to answer — which is what Part 2 is, and which is the only part of this document that will still be
doing work in a month.

**Everything here is veto-able. Nothing here is a decision.**
