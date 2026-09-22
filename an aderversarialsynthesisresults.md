# Adversarial Synthesis — four prongs, one verdict

Reviewer: DeepSeek (Freebuff agent). Date: 2026-09-20.
Inputs, all written independently from the same prompt against the same working tree:

| Prong | File | Length | Its own tally |
|---|---|---|---|
| **DeepSeek** (this prong) | `deepseekadversarialreview.md` | 1,490 lines | 63 findings + 4 corrections + 2 contradictions |
| **GLM 5.3** | `glm5.3adversarialreview.md` | 310 lines | 43 findings (7 D, 18 C, 6 B, 12 A) |
| **Muse Spark** | `musesparkadversarialreview.md` | 715 lines | 43 findings (F-001…F-043) + 32 backlog rows |
| **Luna** | `lunaadversarialreview.md` | 293 lines | 24 findings (F-01…F-24) + 20 backlog rows |

All four read `PROMPT_ADVERSARIAL_PLAN_AUDIT.md`, all four praise the plan's structure, all
four independently reinvented the same replacement cadence (3–5 days committed, each day
adding *"what it expects to learn"*, everything beyond gated by a trigger). None of the four
produced a new 26-day design. That four-fold convergence on the *shape of the fix* is the
strongest result in this synthesis.

**Luna arrived last and changed the picture substantially.** She was written without sight of
the other three (her file is self-contained and references no sibling), and she independently
reached **almost every finding that GLM and Muse missed or actively got wrong** — including
`Fed`, the two constitutions, the banner contradiction, the `OnDestroy` point inside `Kill()`,
the holding-slot modulo defect, the bed-capacity authority split, and the fact that the Farm's
`0.65` curve and the fatigue values are shipped content rather than inventions. Part 5 below is
rewritten to account for this: what was a list of *prong-unique cross-checks* is now mostly a
list of *independently confirmed findings*, and only two findings in this prong remain
uncorroborated by any sibling.

**A warning about what convergence is worth.** The prongs are not independent evidence —
same prompt, same source documents, same four class definitions, same failure modes
available. Agreement raises *gravity*, not truth. `M-28`/`S-1` below is the proof: four
reviewers, and **two of the four believed an invented API was a real seam**, because Fable
wrote it in the same declarative register as the seams that do exist. If the prongs can agree
on a false positive, the plan can too, and so can the next agent who reads it.

**But the split inside that four is diagnostic, not random.** Compare the two who caught
`Fed` with the two who swallowed it, and the difference is a single behaviour:

| Prong | What it did with `Fed(colonist, resource, fraction)` | Outcome |
|---|---|---|
| DeepSeek | Grepped the repository for the symbol before accepting it | Correct (`ENT-01`) |
| **Luna** | Grepped the repository — her Executive finding #1 is *"`Fed(...)` is not present in the codebase; current population consumption is habitation-level and aggregate"* | Correct (`F-06`) |
| GLM 5.3 | Read the draft, saw a signature next to verified seams, called it *"the documented `Fed(...)` seam"* and carried it into its rewrite | Wrong |
| Muse Spark | Read the draft, wrote it into its Day 20 rewrite as *the* mechanism | Wrong |

Catching it required no insight and no extra care — GLM is the most careful of the four
readers of the plan. It required **one grep**, and the two prongs that ran it were the two that
had already decided to check class-A claims against `Assets/` rather than against the packet.
That is the whole argument for the register fix in one row of a table: the plan does not need
smarter readers, it needs the pretenders to be *findable* without a tool.

---

## Part 1 — What is elevated because of the siblings

Elevation rule: a finding rises in priority when multiple prongs reached it from different
starting points, because convergence here measures **how likely the blank is to be
silently filled** rather than how likely it is to be wrong. A blank nobody noticed is a
blank nobody will fill. A blank that three or four reviewers independently named is a blank that
the first agent to open the ticket will fill without noticing — and the sibling who arrived last
changed the picture enough that this document was revised around her.

### Tier 1 — convergence (three or four prongs, same class). Act first.

These are the plan's real debt. Every one is a number or a layout that three or more readers
independently identified as a guess dressed as a decision.

**Luna's confirmation of these rows** (`lunaadversarialreview.md`, written without sight of
this synthesis): `M-01`→`F-07`, `M-02`→`F-08`, `M-03`→`F-19`, `M-04`→`F-03` ("load times" as
experiment input), `M-05`→`F-11`, `M-06`→`F-16`, `M-07`→`F-18`, `M-08`→`F-15`,
`M-09`→`F-05`, `M-11`→`F-02`, `M-12`→`F-23`, `M-13`+`M-14`→`F-13`, `M-15`→`F-11`,
`M-16`→`B-18`, `M-21`→`F-19`, `M-23`→`F-03`/`F-04` (partial), `M-24`→`F-03` (partial),
`M-25`→`F-11`/`F-12` (partial), `M-26`→`F-04`/`F-17` (partial), `M-27`→`F-10`,
`M-30`→`F-20`, `M-32`+`M-33`→`F-21`, `M-19`→`F-19` (partial).

**Luna's coverage has a clear shape, and it is worth naming so the owner knows which prong to
consult for which question.** She is the strongest of the four on **architecture and register**
— she found `Fed`, the two constitutions, the banner contradiction, the `OnDestroy` point in
`Kill()`, the holding-slot defect, and the bed-capacity authority split, all of which GLM and
Muse missed. She is the thinnest of the four on **balance numbers that live inside content
assets**: she does not raise the starting food stock, the 3 Pilots / 2 Farm Techs / 3 Builders
roster (`M-10`), the `Builder` role and `ConstructionRate` values (`M-24`), or the ice-depletion
timing (`M-35`) anywhere in her 24 findings. Those three rows are therefore corroborated by
DeepSeek + GLM + Muse and **not** by Luna — which is not a mark against any of them, but it does
mean the content-balance findings rest on three prongs rather than four, and the owner should
read them knowing which.

| # | The guess | DeepSeek | GLM | Muse | Converged class |
|---|---|---|---|---|---|
| M-01 | Death thresholds: hydration 0 for 24h, nutrition 0 for 72h | `CONST-01` | `D1` | `F-001` | **D** |
| M-02 | Homeless restfulness `0.5` | `CONST-02` | `D2` | `F-002` | **D** |
| M-03 | Four alert thresholds (food 12h, blocked 2h, holding 1h, starved 8h) | `CONST-04` | `D3` | `F-006` | **D** |
| M-04 | Load rates `0.002` h/unit, `0.01` h/passenger | `CONST-08` | `D4` | `F-012` | **D** |
| M-05 | `15°` rotation snapping | `CONST-03` | `D5` | `F-005` | **D** |
| M-06 | `ScenarioDefinition`'s full field list | `ENT-06` | `D6` | `F-016` | **D** |
| M-07 | HR v2 and Colony Report layouts, columns, sections | `UI-01`,`UI-02` | `D7` | `F-025`,`F-026` | **D** |
| M-08 | Allocator algorithm: priority 90, hourly throttle, 4h hysteresis, fill/release order | `ENT-09` | `C13` | `F-018` | **D** |
| M-09 | Ledger schemas: 24h buckets, `Top(20)`, mandated sentence format | `ENT-10` (partial) | `C14` | `F-019` | **C/D** |
| M-10 | Starting stock "48h of food" + roster 3 Pilots / 2 Farm Techs / 3 Builders | `CONST-05`,`CONST-06` | `C11b` | `F-007`,`F-008` | **D** |
| M-11 | Corridor `traversalHours = 0.25` | `CONST-07` | `C9` | `F-009` | **C** |
| M-12 | The clock value inside `[1/60, 1/30]` | `CONST-13` | `C8` | `F-010` | **C** (direction **B**) |
| M-13 | Which construction material exists at all | `CORR-04` | `C17` | `F-031` | **C** |
| M-14 | The `BuildingDefinition` entry list | `CONTENT-03` | `C18` | `F-023` | **D** |
| M-15 | Placement: node-snap vs free, and the rule set | `ENT-12` | `D5`-adjacent | `F-035` | **D** for the rules, **blank** for the mechanism |
| M-16 | Water Processor automated | `CONTENT-06` | `B28`/`C12` | `F-033` | **keep with trigger** — all four say yes, keep, gate it (Luna `B-18`) |
| M-19 | HUD contents and the toast allow-list | `CONST-12` | `C15`,`C23` | `F-027` | **C/D** split |
| M-21 | Hotkeys `Space/1–4/F/Esc` | `CONST-12` | `B27`-adjacent | `F-028` | adjudicated in Part 4 |
| M-23 | Voyage phase enum beyond the core five + `TryAbortToNearestBerth` | **gap** — Part C only, never a numbered finding | `A4b` keep the *shape* | `F-022` | **B** shape / **D** the extras. Mine deferred the queue and abort in the Day 4 window but never *named* the enum as over-specified — a real hole in my audit that Muse filled. Note `S06_ACCEPTANCE.md:20` requires abort-from-`Holding`, so the method is reachable by acceptance even though no player-facing entry point exists on Day 26 |
| M-24 | `Builder` role numbers and `ConstructionRate`'s curve | `CONTENT-04` | `C17`,`C20b` | `F-032` | **D** |
| M-25 | `evaRange` and the whole EVA-link mechanism | `ENT-07`,`ENT-05` | `C10`,`C20b` | `F-015`,`F-024` | **B** need / **C** mechanism |
| M-26 | Socket counts: 2 ports / 2 holding / 8 beds / 4 nodes, etc. | `ENT-11` | `C11` | `F-002`-adjacent | **C** |
| M-27 | `Kill()`'s eight-step unwind order | `ENT-04` | `A9` keep shape | `F-017` | **B** command / **C** order |
| M-30 | `HOW_IT_WORKS.md`'s minute-by-minute narrative | `BLANK-01` | `F-014` | `F-041` | **D** as framing |
| M-32 | The word "locked" (`01_LOCKED_DESIGN`, §"Locked on the spot") | `AUTH-01` | `C24` | `F-036`,`F-038` | **D** as framing |
| M-33 | Constitution/rules + `DECISION_LOG` presented as authoritative | `AUTH-02`,`AUTH-03` | `B31`,`A11` | `F-037`,`F-039`,`F-040` | **D** as framing, rules mostly **A** |
| M-35 | Ice depletes "around day 5–8" | `CONTENT-05` | `C11b2` | `F-043`/`Q-31` | **D** (the number), **A** (the intent) |

**What Tier 1 elevation changes for me.** I had `M-16` (Water Processor) as a lone keep-with-trigger
and `M-24` (Builder/`ConstructionRate`) as "class A for the assets, D for the numbers." Both
siblings land in the same place, which means the *pattern* is real and repeatable rather than
my reading of one paragraph. I am raising both from "noted" to Tier 1 because the pattern
generalizes: **in every single Tier 1 case the code needs a mechanism and the plan wrapped a
number or a layout around it.** GLM states this best and it is the best one-line summary any
of the three produced:

> *"The pattern in every case is identical — a mechanism the code needs (A), wrapped around a
> number or layout only play can teach (D). De-specify the number, keep the mechanism."*
> — `glm5.3adversarialreview.md` §4

That sentence should be the epigraph of whatever version of the plan survives.

### Tier 2 — two-way convergence, and whose version is sharper

| # | The guess | Prongs | Which version wins, and why |
|---|---|---|---|
| M-17 | `ShipFlightProfile`'s physics magnitudes | all three | Adjudicated in Part 4. The **content-asset framing is class A** (all three agree, and GLM/Muse under-credit it); the **magnitudes are D**; the **capture tolerances belong in the profile, not on the port** (my `CONST-09` is the only version that says *where* the guess should live) |
| M-18 | `captureDistance 0.3` / `captureSpeed 0.5` / `captureAngleDegrees 5` | mine + Muse `F-021` | Mine, because it is the only one that separates *what the number is* from *who owns it* — a docking tolerance is a property of the vehicle, so it must not be authored per-port in the scene |
| M-20 | Camera feel (`y = 0` pivot, edge-pan, focus key) | mine + GLM `C22` | Mine on the contradiction (`UI-04`: the camera hard-codes the plane Day 15 forbids hard-coding — GLM and Muse both missed that the plan contradicts itself); GLM on the general "camera feel is the most personal spec there is" |
| M-22 | `SelectableKind`'s 7-value enum | Muse `F-029` only | **Muse's, adopted.** I did not audit the selection enum; "add kinds when their packet lands" is exactly right |
| M-28 | `Fed(colonist, resource, fraction)` — needs as a personal fact | **2 of 4** — mine + **Luna `F-06`** (her highest-priority finding); GLM and Muse accepted the invented API as documented | **DeepSeek and Luna.** See the diagnostic table at the top and `S-1` — the most important line in this synthesis |
| M-29 | How one impaired worker degrades a facility | mine (`ENT-02`) | **Mine.** Muse `F-013` names the curve; only my prong checked `StaffingEffectRule.Evaluate` and found its argument is a *count*, so "contributes a per-worker multiplier" is not currently expressible at all |
| M-31 | `GAPS_AND_OPEN_QUESTIONS.md` answering its own questions | mine + Muse `F-042` | Merged; Muse adds "module kit source" and "identity depth" as separately-listed blanks — adopted |
| M-36 | Stock-policy volumes inside the walkthrough ("keep Water above 20, refill to 60", "export above 10") | GLM `F-014` + Muse | **GLM/Muse novel. Adopted.** I missed these entirely — invented constants smuggled into prose where no table would put them |
| M-37 | Presentation micro-detail: port light colours, `Holding · #1` text, trail ribbon, corridor spline, tumble `0.5–3°/s` | Muse `F-030` | **Muse novel. Adopted (partially).** I flagged only the "two sources for aboard-a-ship" defect; Muse's list is right that a presentation layer at this detail is unearned — though rule 2 makes it cheap to change, which is why I adopt it as low-severity |
| M-38 | `ENVIRONMENT_ASSETS.md` art bible + `EXPLORATION §1–6` read as spec | mine (`BLANK-04`) + Muse `F-043` | Merged; Muse's "add a 'nods, not spec' banner" is the cheap fix, my "keep §8 normative" is the limit |
| M-39 | UI Toolkit over uGUI is untested here | mine (`PREF-03`) + GLM `B27` | Mine, because only my prong proposed the **one-hour spike before Day 8** versus GLM's "cheap to revisit before 6 panels exist" (which is true but does not say how to be cheap) |
| M-34 | The fatigue constants | **not raised by me** (gap); GLM **A** vs Muse **C/D** | **GLM is right, and Luna `F-24` says so independently.** Verified: `Assets/Scripts/ColonyPrototype/People/ColonistStatusComponent.cs:74-77` — the four values are shipped serialized fields, so they are facts. Muse's "the clock retune invalidates them" is also wrong arithmetically (Part 4). Adjudicated in Part 3, R-2 |
| M-49 | How deep the dependency collapse really goes | mine (`SEQ-01`) + GLM §7 | Merged and **GLM's framing adopted, my radius corrected** — see Part 4 |

### Tier 3 — elevated despite being single-prong, because the siblings make them more urgent

- **M-46 (GLM §13) — the trigger cadence has a review-bandwidth cost.** All four prongs
  propose a trigger-gated backlog; GLM is the only one who priced it: *"that's ~1 decision
  cluster per day — heavier than Fable's 'review the observable and go' … the alternative is
  exactly what the owner rejected."* This is not a finding, it is a **cost disclosure**, and
  it belongs on page one of any accepted plan. My `AUTH-05` argued for a one-page veto list;
  GLM notes the *recurring* cost. Elevated to Tier 1 in effect: any plan the owner accepts
  must state what he is signing up to do every day.
- **M-47 (GLM §13) — "blanks are not instructions."** The single best *new mechanism* any
  sibling contributed: working-window tickets must carry literal text such as *"value pending
  backlog #N — ship with placeholder and flag it"* so an agent cannot fill a blank silently.
  My audit said "a blank that fails loudly is better than a guess that passes invisibly"
  (`CONST-08`) but never solved the agent-behaviour problem. GLM did. **Adopted verbatim.**
- **M-48 (GLM §13) — triggers must name observable events, not days**, so a slipped schedule
  cannot panic-fire a backlog item. All three wrote event-based triggers informally; GLM
  states the failure mode. **Adopted as a rule.**
- **M-40 (`PREF-06`) — the unpriced cost of tests-as-deliverables.** Still single-prong. My
  estimate stands: ~40 never-executed test files across 26 days, mandated by tickets, for a
  runner that has never produced a result here. Neither sibling priced it; GLM and Muse both
  *praise* the tests-as-design-artifacts rule. I keep it, but note the disagreement.

---

## Part 2 — What the siblings caught that I did not

Everything below is adopted. Listed with attribution and the specific change to my review.

**1. `Fed()` is not the only invented seam — the walkthrough smuggles numbers `(M-36)`.**
GLM `F-014` / Muse. `HOW_IT_WORKS.md §2` narrates *"keep Water above 20, refill to 60"* and
the Processor's *"export anything above 10 Water"*, and `P0-C` repeats "target 60". I read
those sentences and recorded none of them, because they are in prose in a narrative section
and I was reading prose for *tense*, not for constants. **Change:** my `BLANK-01` (the
walkthrough as framing) gains a second dimension — it is not only framing that anchors
expectations, it is a **hiding place for invented constants**, and it is the best hiding
place in the packet. This makes `BLANK-01`'s "label every verb" fix insufficient on its own:
the narrative also needs every number tagged as placeholder, or moved to a table where the
backlog can see it.

**2. The selection enum and the presentation-detail layer `(M-22, M-37)`.** Muse `F-029`,
`F-030`. I audited `P0-B`'s panels and `P0-P`'s `ColonistView` mapping but not
`SelectableKind`'s seven values nor `P0-S §5`'s port-light palette and beacon text.
**Change:** adopted. Muse's rule is the right one — *"add kinds when their packet lands."*
My audit's nearest equivalent is `ENT-11` (sockets) and these belong in the same family: a
list enumerated for consumers that do not exist.

**3. Constitution rule 6 is a prediction, not an architecture fact `(M-33)`.**
Muse `F-037`. I demoted only rules 10 and 12 and explicitly kept 1–9 as verified facts. Muse
is right about rule 6: `IFacilityPerformanceProvider` has **exactly one implementation**
(staffing), and rule 6 asserts that power, maintenance and morale will fit the same channel —
a claim about unbuilt systems. Verified: staffing is the only provider in `Assets/Scripts`,
and `FacilityPerformanceComponent`'s own comment lists power/maintenance as *future*
providers. **Change:** my `AUTH-02` now demotes rule 6 to *"proposed — revisit when the second
provider ships"* (Muse's trigger, adopted), while **keeping rule 8**, which I reject demoting
in Part 3.

**4. The collapse is shallower than I said, in one specific way `(M-49)`.**
GLM §7. My `SEQ-01` counted *days that consume a decision* and concluded Day 15 propagates to
six days. GLM counts *what kind of artifact must change* and concludes the collapse is
"shallow" because most C/D sit in authored content rather than code shapes. Both are true and
GLM's distinction is the useful one: **re-tuning a content asset is cheap; re-shaping a type
or a prefab is not.** When I merged the two I got a better map than either: the expensive
guesses are exactly `M-06` (scenario schema — the only place a guess hardens into a *type*),
`M-26` (socket counts — prefab and scene churn) and `M-13` (construction material — content
churn across Days 14–17). **Change:** `SEQ-01` gains a "re-tune (cheap) vs re-shape
(expensive)" column, and the "specify these four days least" conclusion narrows to those
three items. This is a genuine correction to my own review: I had overstated the cost of
constant-level guesses.

**5. "Expects to learn" needs a gate, not just a field `(part of the cadence convergence)`.**
GLM §9: *"A day may not be committed to ticket depth until its predecessor's learning is
recorded."* My Part C added the learning field and the "a day that learns nothing is not a
day" line, but a field can be filled with prose. GLM's rule makes the learning **load-bearing
for tomorrow**. **Change:** adopted into Part C's policy.

**6. Muse's `DEATH_SPIKE_ENABLED` idea `(M-01)`, with one condition.** Muse D-01: run needs
with death behind a debug guard, log the curves for 72h, choose thresholds after. My
`CONST-01` said "blank it, add a critical transition" — which produces no data. Muse's version
produces exactly the data the threshold needs. **Adopted, conditioned:** the guard must be
deleted at the Day 26 gate, not shipped — a permanent `DEATH_SPIKE_ENABLED` bool is the
"lying boolean" that constitution rule 9 forbids, and leaving it in would be a *new* class-D
artifact created by the fix.

**7. GLM's A12 elevation of characterization-first tickets.** GLM makes `S00`/`T00` a
protect-list item in its own right ("the plan's best process instinct"). I had them in the
protect list as part of the staffing split; GLM's framing is better because it generalizes:
*map what exists before moving it* is the discovery method inside a locked design. **Adopted**
as its own protect-list entry.

**8. Luna's fifth definition-of-done criterion — the cost of a veto `(new)`.** Luna's
Definition-of-done check adds a criterion none of the other three wrote, and it is the sharpest
single test of whether a de-specification actually worked:

> *"The owner can veto any proposed choice without first unpicking a downstream schema, panel,
or content catalog."* — `lunaadversarialreview.md`

Every other criterion in the packet's §8 measures whether the *reader* can understand the plan.
This one measures whether the *owner* can still move. It is checkable per decision, it catches
exactly the failure mode `SEQ-01` was built to map (a veto that costs six days), and it converts
"is this specified too far ahead?" from an aesthetic judgement into a concrete question: *if I
reject this on Friday, what breaks?* **Adopted as the fifth criterion in Part 6**, ahead of the
test-cost item.

**9. Luna's flight-fidelity axis `(new)`.** Luna's Day 3 experiment asks whether *"the flight
model is worth continuing before committing to 6DOF magnitudes"* — i.e. she makes the model's
*fidelity*, not just its constants, an experiment outcome. My `PREF-04` treated the whole flight
decision as class B because determinism plus the project's pause/disable semantics rule out
PhysX (and DECISION_LOG #13 already flags it). Luna is right that there is a second axis I
collapsed: **the own-integrator requirement is forced (B); the fidelity of that integrator —
full 6DOF with angular velocity and a scalar inertia tensor, versus a kinematic or
reduced-order model with Slerp attitude — is an open experiment (C).** Adopted as a split:
keep `FlightIntegrator` off PhysX, and let the Day 3 characterization report say whether
angular dynamics are visibly earning their complexity at 1× and 10×.

**10. Luna's Day 2 minimality — convert one module, not five `(new, with a caveat)`.** I wrote
Day 2 as "convert the five modules to prefabs" with a reduced socket set. Luna goes further:
*"convert one representative module and one ship, adding only sockets their next experiment
requires."* That is the better principle — Day 2 exists to learn whether the convention holds,
and one module plus one ship proves that as well as five does, at a fifth of the churn. Adopted
with a scheduling caveat she does not resolve: Days 5–6 need all five prefabs (voyage
migration and the corridor acceptance run on them), so the rule becomes *convert one to prove
the convention, then convert each remaining module on the day its first consumer lands* — which
is the same rule the backlog already applies to sockets (`ENT-11`).

**11. Luna's trigger wording for the needs fork `(new)`.** My `ENT-01` trigger was "a shortage
happens and you want to know *who* is hungry, not *that* the colony is." Luna's is operational
in a way mine is not: *"run a shortage playtest and record whether individual attribution is
wanted."* The difference matters because mine asks the owner to notice a feeling and hers asks
him to *write down an answer*, which is the whole point of a learning loop. **Adopted**, and
applied to every trigger in the merged register: a trigger should name what gets *recorded*,
not only what gets *observed*.

---

## Part 3 — What the siblings suggest that I reject, and why

Five rejections and two partial rejections. Reasons are evidence-based, and where the evidence
is code, it is cited.

### R-1. REJECT — making `ModuleSockets.beds.Count` the authority for bed capacity

*Proposed by:* Muse D-02 ("`HabitationComponent` with `[SerializeField] List<Transform> beds`
**authority** for capacity") and GLM `C11`/`A10` (bed/socket counts as authored-on-prefab).
*Opposed by:* DeepSeek `CONTENT-02` ("`HabitationComponent.capacity` stays authoritative; the
socket list *displays* up to capacity; disagreement logs an authoring error") **and Luna `F-17`,
independently**:

> *"`HabitationComponent.capacity` is the runtime authority today. Making
> `ModuleSockets.beds.Count` the economy silently lets art edits change housing."*
> — `lunaadversarialreview.md` `F-17`

So this is a genuine **2–2 split**, not a lone objection, and Luna supplies the failure mode I
had only inferred: an art edit silently changing the economy.

Why I hold: `HabitationComponent.capacity` is an authored, serialized `int` in Runtime today
(`Assets/Scripts/ColonyPrototype/People/HabitationComponent.cs:8`), and `ModuleSockets` is a
convention whose stated purpose is presentation — `meshRoot` for visuals, `workstations` and
`beds` for the Facilities presenter to place bodies in. Making the sim's `capacity` a
`Count` over a transform list hands the **economy** to a list an artist edits. Concrete
failure mode: someone deletes a bunk while making the art read better and quietly changes how
many colonists can sleep — with no diff in any file that looks like a design change. That is
constitution rule 4 ("one authority per fact") inverted. The disagreement is narrow and I would
take the veto: the plan can set capacity *from* the socket count in an `OnValidate` when the
prefab is authored (that is a convenience), but the fact must live on the Runtime component with
a read-only accessor. **Luna's `F-17` is the tie-breaker worth weighing most**, because she
reached it by asking who owns the fact rather than by reading the two prongs that disagreed —
and she made it her *last responsible moment before prefab conversion*, which is earlier than
Muse or GLM placed theirs.

### R-2. REJECT — blanking the fatigue constants

*Proposed by:* Muse `F-003` (class C: *"values are plausible but informed by 24-second day
that no longer exists… Clock retune invalidates them"*).
*Opposed by:* GLM (cites them as **recorded facts from the codebase** — correct) **and by both
DeepSeek and Luna**, whose prongs each independently made the general rule explicit before this
synthesis existed:

> *"The Farm's `0.65` staffing curve and fatigue values are existing serialized content, not
> invented constants. … The audit must not erase existing authorities while removing future
> guesses."* — `lunaadversarialreview.md` `F-24`, her Executive finding #5

So Muse is alone on this one, **1–3**, and the reason is worth preserving: it is the *only*
finding in four prongs where the error is in the safe direction (over-de-specification), which is
why it is worth naming at all — the audit can fail by deleting a fact, not only by keeping a
guess.

Why I reject Muse: **verified in code.** `ColonistStatusComponent.cs:74-77`:
`workFatiguePerHour = 0.10f`, `sleepRecoveryPerHour = 0.10f`, `exhaustionThreshold = 0.90f`,
`recoveredThreshold = 0.20f` — shipped serialized fields, live in the scene, with
`AdjustFatigue`/`RefreshExhaustionLatch` implementing the latch. They are **not** Fable's
invention; they are the codebase, and the plan quotes them the way `PLANNING_REVIEW_FACTCHECK`
quotes line counts. Muse's "the clock retune invalidates them" is also arithmetically wrong:
every one of those rates is per **game-hour**, so a change to real-seconds-per-game-hour
leaves the math identical — it changes only how long the *player* watches a shift drain.
**De-specifying a fact is the mirror-image of the disease this audit is about**, and it is the
same mistake `CORR-01` catches in the audit prompt itself. What I do accept from Muse: the
*scene-authored* values deserve a look at the new clock, so I add a one-line **revisit
trigger** ("after two shifts with walking commutes, ask whether exhaustion arrives at a
readable moment") — a re-tune, not a blank.

### R-3. REJECT (partially) — reopening the hybrid control model

*Proposed by:* Muse `F-034` ("the *shape* — per-role-per-shift target grid + 1–10 priority +
pin — before manual HR is D") and `F-033`'s framing.
*My position:* `PREF-02` — the control model is class **A** because it is the owner's own
decision, evidenced by `DECISION_LOG.md` #6 marking it "User choice."

All four prongs blank the allocator's *algorithm*, and that is agreed (`M-08`). Where I
diverge from Muse is the step past the algorithm: reopening whether *targets + priority +
pin* is the right shape makes the owner re-decide what he already decided, which is the
opposite failure to premature specification and just as expensive. He said it; it is
ratified by being his. What *is* open, and what all three agree on, is that a manual screen
must exist and be used before the allocator is built — that is a sequencing question
(my `ENT-09` split, GLM `C13`, Muse `Q-16`), and it is strictly better than reopening the
model. **Rejected as proposed; the sequencing half is adopted.**

### R-4. REJECT — demoting constitution rule 8

*Proposed by:* Muse `F-037` (demote rules 6, 8, 10).
*Accepted:* rule 6 (see Part 2 item 3) and rule 10 (already in my `AUTH-02`).
*Rejected:* rule 8 ("a new facility is composition + content, never a bespoke controller").

Why: rule 8 is **verified by the project's own evidence**, not predicted.
`STATE_OF_THE_PROJECT.md` lists under "What exists and is good": *"New facility = composition
+ assets; proven by test"*, and the content layer's seven definition types are what make it
true. It is a statement about a kit that already works, not about power or morale. Muse's
argument for demotion ("pre-decides solutions for systems that have never been built") does
not apply: rule 8 governs *facilities*, and facilities have been built this way. Keep it
locked. Rules 1–5, 7, 9, 11, 12 also stay (with the *framing* of 10 and 12 moved to working
conventions, per my `AUTH-02`).

### R-5. REJECT — writing the backlog and the rewritten plan as files now

*Proposed by:* GLM ("full register, **ready to commit as a file**", "propose to create next"),
Muse ("`DECISION_BACKLOG.md` — propose to create next").
*My position:* all three deliverable sets live **inside** the review files until the owner
says otherwise. The prompt asks for deliverables; the working tree is the owner's, and the
repository root already carries six plan documents plus four review artifacts. Adding
`AUDIT_FINDINGS.md`, `DECISION_BACKLOG.md`, `PLAN_SKELETON.md` and `PROTECT_LIST.md` is four
new top-level files **before the owner has accepted a single finding**, which is precisely the
"commit before it is earned" pattern all four prongs spent ~2,800 lines objecting to. It would
also be self-refuting in the most literal way available. **Rejected** — and I note that GLM's
and Muse's versions are each self-consistent enough that the owner can lift them wholesale
the moment he wants them.

### R-6. PARTIAL — `EXPLORATION_AND_LONG_RANGE.md` §1–6 to "nods, not spec" (M-38)

*Proposed by:* Muse `F-043`. *Accepted:* the banner demotion. *Rejected:* treating §1–6 as
something to move out of the Phase 0 packet.

Why: §8's seams are *justified by* §1–6 — "keep `FreightSupply` vehicle-agnostic" only makes
sense if you can see the Phase 2 conduit it is protecting, and "`operatingRole == null` means
automated" only makes sense with probes on the horizon. Strip §1–6 and §8 becomes nine
unmotivated instructions that a future agent will rationalize away. Keep them where they are,
demote the **register** (banner plus "horizon, not input" on each item), and keep §8
normative. This is the same fix I applied to `ROADMAP` in `BLANK-03`, applied consistently.

### R-7. PARTIAL — GLM's "the collapse is shallow"

*Accepted as a correction to my radius (`SEQ-01`).* *Rejected as a conclusion.* The
mechanical collapse is shallow **because Fable fronted the seams correctly** — additive
scenes on Day 1, `ModuleSockets` on Day 2, the staffing split on Days 3–4. That is a genuine
credit and GLM states it well. But "shallow" is only true for constants. Three items are
shape changes with real churn (`M-06`, `M-13`, `M-26`), and there is a fourth cost neither
sibling priced: **the schedule's own dependency structure means a blanked Day 15 cannot
commit Day 16 until the placement question is answered** — so the rolling cadence absorbs the
collapse as *time*, not as rework. GLM's "shallow" is optimistic about the shape of the
conversation the owner will actually have every day (see `M-46`).

### R-8. NOTE — Luna produced nothing to reject, and that is itself a finding

I went looking for a fourth rejection and did not find one worth recording. Luna's 24 findings
and 20 backlog rows are adopted in full, with exactly one caveat (her Day 2 minimality leaves the
remaining four prefab conversions unscheduled — item 10 above, and it is a gap rather than a
disagreement). This is worth stating because of *what she converged on*: she did not merely
repeat the sibling findings, she independently arrived at the *positions* this prong holds when
the other two disagreed — `Fed` is not a seam (`F-06`), bed capacity stays on the Runtime
component (`F-17`), the fatigue values are facts (`F-24`), the audit prompt's own seed list is
wrong (`F-24`/Executive #5), and the veto path must stay cheap (DoD item 5). Four prongs, two
axes of the same document, and the one prong that read for *ownership* rather than for
*register* agreed with the prong that grepped the code — on every point where the other two
differed. If the owner wants a single heuristic for triaging the four reviews: **when DeepSeek
and Luna agree and GLM and Muse agree, trust the pair that cited a file.**

---

## Part 4 — Reviewer-versus-reviewer disagreements, adjudicated

GLM's own §13 predicted the three richest disagreements. All three are real; here are the
verdicts, plus four GLM did not anticipate — two of which only became visible once Luna landed.

| # | Question | GLM | Muse | DeepSeek | Verdict |
|---|---|---|---|---|---|
| 1 | Is the clock retune class A or B? | A (direction), C (value) | B/C (direction needed, 60× unproven) | B, correctly flagged for veto | **A for the direction, C for the value.** All four agree in substance; the labels differ only in which half each prong fronted. GLM's label is cleaner. My `CONST-13` keeps the *governance* point (one inspector field + the "no real-second constants" rule) that neither GLM nor Muse made — and Luna `F-23` states it independently |
| 2 | Are UI constants C or D? | C with very late triggers | D, except HUD's population/food-on-water as B | D (delete from tickets) | **Split by a test: can the artifact function without a value?** If yes → **D** (hotkeys, column order, toast allow-list, alert threshold *values* with empty defaults). If no → **C with a placeholder** (camera rig parameters, ledger bucket width, `SelectableKind`'s core four). This resolves GLM's and Muse's disagreement rather than picking a side, and it is checkable per item |
| 3 | Should the scenario *principle* be gated too, or only the schema? | Gate only the schema; keep bootstrap-through-completion as A | Gate the schema; derive minimal fields by spike | Gate the schema; **derive the whole asset from the live scene** | **DeepSeek's goes furthest and I hold it.** GLM and Muse both keep a hand-authored asset and blank its fields; I remove the hand-authoring entirely (capture-what-you-built, replay-what-you-captured). Their version still requires someone to *decide what to write down*; mine never asks. Both siblings' versions are compatible with mine as a first step — but if the owner takes only one, take the round-trip, because it is the only version where the schema cannot drift from construction (`ENT-06`, `SEQ-03`) |
| 4 | *(not anticipated)* Fatigue constants | A — recorded facts | C — invalidated by the clock | not raised | **GLM.** Verified in code, and Muse's arithmetic is wrong — Part 3, R-2 |
| 5 | *(not anticipated)* Who owns bed capacity | socket counts (C/A) | sockets are authority (D-02) | Runtime `capacity` is authority | **DeepSeek.** Part 3, R-1 — the only place I believe two prongs are wrong together |

**Luna's positions on all five adjudications** — added after she landed, and note that she was
written with no sight of this file or the other two prongs:

| # | Question | Luna's position | Effect on the verdict |
|---|---|---|---|
| 1 | Clock: A or B? | **A for the need, C for `1/60` vs `1/30`** (`F-23`) | Nothing changes — this is her third-independent arrival at "direction forced, value is a knob," matching GLM and me. But she also states my `CONST-13` protect rule almost verbatim: *"Do not express later decisions in real seconds; all durations remain game-hours/game-seconds so the clock can be revisited cheaply."* Two prongs arriving at that governance rule independently is the clearest sign it is load-bearing |
| 2 | UI constants: C or D? | **D** for hotkeys and alert values (`F-19`: *"implementation defaults, not earned decisions"*), **infrastructure stays** | Luna and I land on the same side against GLM's "C with very late triggers," and her phrasing is the better statement of the same test |
| 3 | Gate the scenario schema, or the whole principle? | **Round trip**: *"run a capture/replay spike: capture the live base into the smallest asset needed to replay it through the same creation path"* (`F-16`, and Part E's Day 22 rewrite) | **Moves from 1-vs-2 to 2-vs-2.** Luna lands with me against GLM and Muse, so the "derive the asset rather than author it" version is no longer my preference alone |
| 4 | Fatigue constants | **A — shipped content** (`F-24`, Executive #5) | Muse is now alone, **1–3** (Part 3, R-2) |
| 5 | Who owns bed capacity | **Runtime `capacity`; sockets are visual slots; mismatch is an authoring error** (`F-17`) | **Moves from 1-vs-2 to 2-vs-2.** See Part 3, R-1 |

**Two disagreements GLM did not anticipate, both surfaced by Luna.**

**6. Is the own-integrator forced, or is the flight model itself on trial?** GLM `A4b` protects
the phase-machine *shape* as class A; Muse `F-011` calls the physics block "exhaustive constants
for a ship never flown" and blanks it; Luna `F-03` asks *"whether the flight model is worth
continuing before committing to 6DOF magnitudes"*; my `PREF-04` called the whole decision class
B because determinism plus the project's pause/disable semantics rule out PhysX. **Verdict:**
Luna found the real seam. There are two axes here and three of the four prongs collapsed them:
**the own-integrator requirement is forced (B)** — PhysX is not pause- or speed-safe, and the
readiness packet made those semantics load-bearing — while **the fidelity of that integrator is
open (C)**: full 6DOF with angular velocity and a scalar inertia tensor versus a reduced-order
or kinematic model. Adopted as split in Part 2, item 9.

**7. Does the plan need a fifth definition-of-done criterion?** Luna says yes and proposes one
(**the cost of a veto**: *"the owner can veto any proposed choice without first unpicking a
downstream schema, panel, or content catalog"*). No other prong wrote an equivalent, and the
packet's own Definition of Done has only three questions. **Verdict:** Luna's, adopted — it is
the only criterion in four reviews that measures the owner's *mobility* rather than the reader's
comprehension, and it is checkable decision-by-decision. See Part 2, item 8.

**On the adjudications where this prong was originally alone:** Luna's arrival changes the
count from "lone prong" to "the side that cited a file." In R-1 the seam exists but has an
owner the two dissenting prongs did not check; in R-2 the "fact" is a real fact and one prong
mistook it for a guess. Both errors have the same root, which is also `M-28`'s root and this
audit's thesis: **the packet's single register makes real seams, invented seams, and shipped
constants indistinguishable at reading speed** — and the only reliable way out is to check the
code, which two of the four prongs did on the seams and one of the four did on the constants.

---

## Part 5 — Non-convergent findings, and Luna's third-party confirmation

A synthesis that only reports its own absorption is not a synthesis, so this part lists the
findings that did **not** appear in all four prongs — with Luna's status on each, since she
arrived last and changed most of them from "lone objection" to "two-prong finding."

**Headline: of the eight findings this prong originally listed as unshared, Luna independently
reached six.** Across this prong's whole audit, only two findings remain uncorroborated by any
sibling: `S-5` below (one scenario, three bed counts), and `UI-04` in the review file (the
camera hard-codes the `y = 0` plane on Day 7 that Day 15 forbids hard-coding). Neither is a
judgement call; both are a grep. That is the right residue for four independent reviews to
leave behind.

| Finding | Status | Who corroborates it |
|---|---|---|
| `S-1` — `Fed` is not a seam | **2 of 4** | DeepSeek, Luna `F-06` |
| `S-2` — the audit prompt's seed list is wrong | **2 of 4** (+prompt wrong) | DeepSeek, Luna `F-24` |
| `S-3` — two divergent constitutions | **2 of 4** | DeepSeek, Luna `F-21` |
| `S-4` — banners contradict their headings | **2 of 4** | DeepSeek, Luna `F-21` |
| `S-5` — three bed counts in one scenario | **1 of 4** | — |
| `S-6` — holding-slot modulo overlap | **2 of 4** | DeepSeek, Luna `F-22` |
| `S-7` — `Kill()`'s test passes before `Kill()` exists | **2 of 4** | DeepSeek, Luna `F-10` |
| `S-8` — an open question resolved in a ticket | **2 of 4** + GLM in passing | DeepSeek, Luna `F-13`, GLM `C17` |
| `UI-04` — camera `y = 0` vs "never hard-code y = 0" | **1 of 4** | — |
| `ENT-02` — impaired-worker aggregation is not expressible | **2 of 4** | DeepSeek, Luna `F-09` |
| `ENT-05` — deposit auto-selection is unearned autonomy | **2 of 4** | DeepSeek, Luna `F-14` |
| `ENT-08` — `SitePlane` is over-generalized | **2 of 4** | DeepSeek, Luna `F-12` |
| `CONST-11` — the line target is not a product requirement | **2 of 4** | DeepSeek, Luna `F-01` |
| `CONST-13` — "no duration expressed in real seconds" | **2 of 4** | DeepSeek, Luna `F-23` |
| `PREF-03` — UI Toolkit needs a one-hour spike | **2 of 4** | DeepSeek, Luna `B-15`, GLM `B27` (partial) |
| `PREF-06` — the unpriced cost of tests-as-deliverables | **1 of 4** | — |

Two of the sixteen above are single-prong, and both are *checkable facts* rather than opinions
(`S-5`, `UI-04`). One more, `PREF-06`, is an estimate no sibling priced at all — so if the owner
wants to act on a single finding without waiting for agreement, the safest candidates are the
three in the bottom row: run the two greps, and count the test files the packets mandate.

**S-1. `Fed(colonist, resource, fraction)` does not exist; two of the four prongs treated it as
a real seam, and two did not.** This is the most important line in this document.
- Fable wrote it once: `tickets/P0-D_Live_And_Die/00_DRAFT_DESIGN.md:8`.
- GLM's de-specification pass then wrote: *"fed from `PopulationResourceConsumer` satisfaction
  via the **documented** `Fed(colonist, resource, fraction)` **seam**… (all A-class seams)"*
  (`glm5.3adversarialreview.md:260`).
- Muse's rewrite then wrote: *"`PopulationResourceConsumer` reporting `Fed` per tick"*
  (`musesparkadversarialreview.md:588`).
- Grep for `\bFed\b` across the **entire repository** (run before this synthesis existed):
  seven hits — Fable's draft, my review, GLM's review, Muse's review. **Zero in `Assets/`.**
  Re-running it now returns ten, six of which are these four documents quoting each other,
  which is itself a small illustration of how a guess acquires a paper trail.

So an audit whose entire thesis is *"a plausible-looking line in this packet may be an
assumption, not a fact"* had **two of its four prongs** adopt an invented API into their
*replacement* designs, calling it "documented" — and both prongs that caught it did so with a
single grep (Luna `F-06`: *"`Fed(colonist, resource, fraction)` is not present in the codebase;
current population consumption is habitation-level and aggregate"*, her highest-priority
finding). The four-way split and what it says about the plan's register is broken out in the
table at the top of this document. The mechanism is worth naming precisely,
because it is the failure the owner described and it does not require anyone to be careless:
`Fed(colonist, resource, fraction)` has exactly the *shape* of the real, verified seams it sits
beside (`StaffingComponent.ActiveWorkersChanged`, `ReadinessHistory.OnRecorded`), it appears
in a section of a document whose other technical claims were fact-checked to the line count,
and reading for it costs a grep nobody ran because two other prongs had already accepted it.
Per my `ENT-01`, the real component is **habitation-level**: `SimulationTick` consumes
`residents × amountPerResidentPerGameHour × delta` against one inventory
(`Assets/Scripts/ColonyPrototype/People/PopulationResourceConsumer.cs:25-45,74-93`), and its
`ResidentCount` comes from `HabitationComponent`. It *cannot* name a colonist. This makes
"are needs personal or aggregate?" a **rewrite-level fork** (my `ENT-01`, restored to Tier 1)
rather than a seam everyone can agree on — and it is the strongest available argument for the
register fix, because the fix is not "be more careful," it is "make the pretenders
findable."

**S-2. The audit prompt's own seed list contains an error (`CORR-01`), and Muse repeated it.**
The prompt lists "the Farm's production curve '0.65 for one tech'" as invented content. It is
shipped: `Assets/GameData/Roles/FarmOperator.asset:26`, evaluated by
`StaffingEffectRule.Evaluate`. Muse `F-004` lists it as class D again. GLM avoided it (GLM's
worked example correctly treats fatigue as recorded fact, which is the same instinct). Fable
was *quoting existing content* — so areader who acts on the prompt or on Muse would "blank" a number that exists, which is the same
class of error as R-2 and just as costly. Anyone synthesizing further should strike that seed.
**Confirmed independently by Luna `F-24` and her Executive finding #5**, which states the rule
generally: *"The audit itself must protect facts. The Farm's `0.65` staffing curve and fatigue
values are existing serialized content, not invented constants. They are not findings merely
because they are numbers."* So the score is two prongs right, one prong repeating the error, and
the prompt itself wrong.

**S-3. Two divergent constitutions on disk (`CORR-02`).** `ARCHITECTURE_CONSTITUTION.md`
(canonical, per `START_HERE.md`) and `ROADMAP.md §1` (the "summary" that wins ties) differ
materially: the ROADMAP omits the ship-pose clause of rule 4, names a type that does not exist
(`ShipPhase`; the code has `ShipMovementPhase`), and drops rule 12's test clause. Neither
sibling noticed. Every packet README pastes "the constitution," so two copies grade tickets
differently. **Confirmed by Luna `F-21`**, who found the same duplication and the same
non-existent `ShipPhase` name, and who adds the fix's scope in one clause: *"demote only
unearned rules, not proven runtime invariants."* That is the line Part 3, R-4 defends.

**S-4. Every bannered document contradicts its own headings (`CORR-03`).** The 2026-09-20
banner pass is praised by all four prongs (correctly — it is the strongest act in the packet);
GLM and Muse did not notice that `ARCHITECTURE_CONSTITUTION.md` now says "proposals" three lines
above "this file wins," `GAME_DESIGN_DECISIONS.md` says "nothing here is locked" directly above
`## Locked`, and `HOW_IT_WORKS.md` says "as proposed" above a scene written in the present
tense. This is a one-pass fix and it is the cheapest credibility win available. **Confirmed by Luna
`F-21`**, who ties it to the same root cause her report names (`D framing plus documentation
defect`). Two of four prongs found it; the other two praised the banner pass without noticing
that it contradicts the headings directly beneath it.

**S-5. One scenario, three bed counts (`CONTENT-01`).** Four documents say the Command Pod has
8 beds, two say 6 — including Day 21's observable, which *depends* on 6. Muse's D-02 rewrite
uses "8 colonists / 6 beds" without flagging that the same plan promises 8 beds elsewhere. The
number matters because 8 beds removes the housing pressure Day 21 exists to demonstrate.
**Still single-prong.** Luna's `F-17` is about *who owns* capacity, not about the 6-versus-8
inconsistency itself, and neither GLM nor Muse flagged it — Muse's D-02 rewrite simply uses
"8 colonists / 6 beds" as if both figures came from one document. This is one of only two
findings here that no sibling corroborated.

**S-6. Holding slots put two ships in the same place (`ENT-13`).** `slot index = queue
position mod slots` with one slot authored per station (Farm, Water Processor) means a second
queued ship occupies the first ship's pose. Day 6's own observable ("duplicate the shuttle →
one holds at the single Farm port") creates that state. A precise formula that hides a visual
defect is the audit's cleanest example of the prompt's rule that *"precision is not
justification."* **Confirmed by Luna `F-22`**, whose text is nearly identical and who names the
sharpest part of it: *"The very observable intended to prove queueing creates the collision."*
Two prongs, one defect, the same sentence.

**S-7. `Kill()`'s acceptance test would pass today (`ENT-04`).** `ColonistAgent.OnDestroy`
already unregisters from `PopulationManager` and `StaffingManager`, so "the registries no longer
contain them" is true before `Kill()` is written. Muse `F-017` comes close ("the 8-step list
assumes systems that haven't landed") but neither GLM nor Muse caught that two of the eight steps
already exist and therefore prove nothing. The real target is the holders that do *not*
self-clean: contract passenger lists, the carrier, the pilot lease, and open ledger intervals.
**Confirmed by Luna `F-10`**, who reached both halves independently — the invariant formulation
(*"after `Kill`, no contract/carrier/lease/transit/ledger holder still references the colonist…
and no duplicate cleanup occurs"*) and the `OnDestroy` observation ("the current `OnDestroy`
already handles some unregistering"). Her phrase *"no duplicate cleanup occurs"* is better than
mine: it names the failure that a doubled unwinding step would actually produce.

**S-8. An open question the plan resolves inside a ticket (`CORR-04`).**
`GAME_DESIGN_DECISIONS.md §Open` asks which construction materials exist first, under a header
that says "do not resolve inside a ticket"; Day 14 resolves it. Neither sibling found the
contradiction (GLM `C17` notes it in passing: *"the plan answers in one place what it leaves
open in another"* — credited). **Confirmed by Luna `F-13`** in one clause of her build-content
finding: *"Day 14 currently answers an open question that the open-question document says not to
resolve in a ticket."* Two prongs plus GLM's passing note.

---

## Part 6 — The merged action list (smallest set that is worth doing)

Tier 1 is deliberately mechanical: none of it requires deciding anything, which is the only
kind of fix available before the plan is accepted.

**Tier 1 — register fixes, one sitting, zero decisions required.**
1. Strike the Farm-curve seed from `PROMPT_ADVERSARIAL_PLAN_AUDIT.md §4` (`CORR-01`, `S-2`).
2. Delete `ROADMAP.md §1`'s rule list; link to `ARCHITECTURE_CONSTITUTION.md` (`CORR-02`, `S-3`).
3. Fix the banner/heading contradiction in all ten bannered documents: `## Locked` →
   `## Proposed — binding once you say so`; `01_LOCKED_DESIGN.md` → `01_WORKING_DESIGN.md`
   (`CORR-03`, `S-4`, and all four prongs' "locked" finding `M-32`).
4. Remove the Recommendation column from `GAPS_AND_OPEN_QUESTIONS.md` and
   `GAME_DESIGN_DECISIONS.md §Open`; rows become backlog items (`BLANK-02`, `M-31`).
5. Delete the Command Pod bed-count contradiction by making one document the source and having
   the others quote it; pick 6, because Day 21's observable requires pressure (`CONTENT-01`, `S-5`).
6. Demote constitution rules 6, 10, 12 (keep 8 — Part 3, R-4) to `## Working conventions`, rule 6
   with the trigger "revisit when the second provider ships" (`AUTH-02`, `M-33`, Muse `F-037`).
7. Add to every packet README and every ticket the sentence GLM wrote: *"a value pending backlog
   item N ships as a placeholder that is flagged, never as a filled blank"* (`M-47`).
8. Tag every invented number in `HOW_IT_WORKS.md` (start with the stock-policy volumes, `M-36`)
   and every constant in the packet as `placeholder` or `fact`, per `HOW_IT_WORKS` §2's rewrite
   contract (`BLANK-01`, `M-30`).
9. **Adopt Luna's fifth definition-of-done criterion** (Part 2, item 8) as the gate every
   remaining ticket is checked against: *"the owner can veto any proposed choice without first
   unpicking a downstream schema, panel, or content catalog."* It costs one question per ticket
   and it is the only criterion that measures whether the de-specification worked rather than
   whether it reads well.
10. **Caveat Luna's Day 2 minimality** before it is used as written (Part 2, item 10):
    converting one module and one ship proves the convention, but Days 5–6 need all five
    prefabs, so the ticket must say *convert one now; convert each remaining module the day its
    first consumer lands.* Otherwise Day 5 arrives with one usable module.

**Tier 2 — the merged backlog.** The four prongs produced 63 + 43 + 43 + 24 findings against
overlapping topics; deduplicated they are **35 distinct questions** (Tier-1 and Tier-2 rows
above plus the `S-*` and `R-*` items). One register, one owner's column, one trigger per row,
each trigger an observable event rather than a day (`M-48`) and each naming what gets
**recorded** rather than only what gets observed (Luna's wording, Part 2 item 11). GLM's 26-row
version, Muse's 32-row version and Luna's 20-row version are each ~90% the same list and any of
them can serve as the starting sheet; the merged set adds only `M-29` (the
effectiveness-aggregation semantic), `M-47`/`M-48` (agent-facing rules rather than design
questions), `M-24`/`M-35` (which Luna did not cover) and `S-1`'s personal-vs-aggregate fork.

**Tier 3 — the rolling window.** All four prongs converged on the same cadence and the same
daily template; the merged version is: work · observable · **what it expects to learn** ·
**which backlog triggers today's result informs** · commit per ticket. Working depth capped at
3–5 days. *"A day may not be committed to ticket depth until its predecessor's learning is
recorded"* (GLM §9). Days 1–5 as in my Part C, with two sibling additions folded in: Muse's
Day 1 learning question (*does domain reload preserve cross-scene singletons and the five
`FindObjectsByType` startup paths?* — a real risk neither I nor GLM named), GLM's three-value
flight hypothesis report on Day 3, Luna's flight-fidelity question on the same day (Part 2,
item 9) and Luna's Day 2 minimality with its caveat (item 10).

**Tier 4 — the two things to fix before any of it, because they are cheap and they compound.**
The UI Toolkit spike (`PREF-03`, one hour, before Day 8 is spent on the assumption) and the
Day 19 *measurement* rather than tuning (`SEQ-04`, which makes `M-01`, `M-03`, `M-10` and
`M-35` self-answering).

---

## Part 7 — Residual risks that survive all four prongs

GLM named two that neither of us should have left out, and the three together leave one open.

1. **The trigger cadence costs the owner time every day** (`M-46`). A trigger-gated plan is
   *more* human work than Fable's "review the observable and go" — roughly one decision cluster
   per day. That is the price of the method, and it should be stated on page one rather than
   discovered at the first trigger. **Luna's fifth definition-of-done criterion is the cheapest
   available mitigation for this cost, and it is the only one any prong proposed:** a plan where
   every choice can be vetoed without unpicking what depends on it is a plan that can absorb one
   decision per day, because each decision stays one decision. Where a veto cascades — the
   scenario schema, the bed-capacity authority, the construction catalog — the daily cadence
   becomes a daily re-plan, and that is the real reason those three items sit in Tier 1.
2. **Blanks are not instructions** (`M-47`). Without explicit placeholder language, agents fill
   blanks silently — which is how we got here.
3. **Open across all four prongs: nobody verified the art or the environment numbers.** I
   declined to (`ENVIRONMENT_ASSETS.md`'s fog parameters, `Texture3D` sizes, LOD targets, palette
   hexes); GLM and Muse treated them as register problems rather than technical ones; Luna's
   nearest finding is `B-16` (interior visibility and module kit), which is a *scope* question
   rather than a correctness one. So the environment packet is the one area where **no prong has
   an opinion about correctness** — it should be audited on its own terms or accepted as-is,
   explicitly.
4. **Open across all four prongs: they share a common-cause error.** Same prompt, same sources,
   same class vocabulary, same four definitions. `S-1` proves the error is live and shows what
   breaks it: Luna is the closest thing this exercise has to a control (written with no sight of
   the other three) and she still shares the prompt and the documents, so her agreement is
   evidence about the *plan* rather than evidence about the *method*. A genuinely independent
   expert reading **only** `Assets/` and the owner's own words — never the plan — would be the
   honest control experiment, and it is still the only audit nobody has run.

---

## Part 8 — Verdict

**Fable's plan is not wrong, and it is not badly made.** All four prongs say a version of that,
independently, and each spent its own length better defending the plan's structure than
attacking it — Luna's verdict is the plainest of the four: *"The failure is register, not
competence."* The structure survives this synthesis essentially intact: the additive scene
split, the staffing split behind an unchanged facade, `ModuleSockets` as a convention, the
Dijkstra-over-authored-links resolver, the single voyage authority, publish-then-commit,
construction-as-composition, extraction outside freight arbitration, the daily observable, and
the dependency-flaw discipline F1–F14.

**What does not survive is the register.** Roughly a third of the plan's decisions are guesses
about systems nobody has built, written in exactly the same voice as the facts that were
verified to the line count — and the consequence is now demonstrated rather than argued: **two
of four adversarial reviewers, each performing this audit deliberately, adopted an API that
exists nowhere in the repository into their own replacement designs and called it a documented
seam** (`S-1`).

The fix that both the rejections and the adoptions point at is not a new plan, and two prongs
stated it independently in almost the same words. GLM's version is the mechanism:

> **A mechanism the code needs, wrapped around a number or layout only play can teach. Keep the
> mechanism, blank the number, and say out loud which one is which.**

Luna's version is the purpose, and it is the better closing line because it says what success
looks like rather than what to do:

> **"The plan becomes trustworthy when its blank spaces are visible enough that the next agent
> cannot mistake them for permission to design."**

That is the whole audit, and the `Fed` row is why both sentences are needed: GLM's tells you how
to write the plan, Luna's tells you how to tell whether you succeeded. On `Fed`, the plan read
trustworthy and wasn't; two agents took the blank for permission.
