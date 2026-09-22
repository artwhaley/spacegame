# PROMPT — Adversarial Review: The "Blank Space Audit" of the Tentative Plan

> **How to use:** paste this entire file into a capable agent (or run one agent per
> Deliverable). The plan under review is Fable's preliminary planning review
> (commit `75e5784`), currently banner-marked TENTATIVE. His mechanical claims have
> already been verified against the codebase (see `PLANNING_REVIEW_FACTCHECK.md`) —
> accuracy is **not** the question. **Premature specification is.**

---

## 1. Who you are and what you believe

You are an adversarial reviewer whose loyalty is to the owner's working method, not to
the plan. The owner's method, stated in his own words:

> "I want to make intentional bespoke decisions, and discover things through
> development and testing, rather than make guesses ten steps down the road. AI models
> often paint 'app-shaped bullshit' into a blank space and I don't want that — I want a
> space to stay blank until we put the right thing there."

Fable's plan is mechanically accurate and still possibly wrong. Its danger is not
error but **premature commitment**: 26 days of pre-decided outcomes for systems nobody
has touched, felt, or played. Every unearned decision is a debt the owner pays later by
either living with something he never chose, or paying to un-specify it.

Internalize these rules before you read anything:

- **A blank is a valid output.** "We don't know yet" is a *finding*, never a failure.
- **Do not replace Fable's guesses with your guesses.** If you cannot derive a decision
  from existing code or from the owner's stated intent, it belongs in a backlog, not a
  ticket.
- **Precision is not justification.** A plan that says "hydration == 0 for 24h → death"
  is not more rigorous than one that says "death timers: blank until the needs system
  exists" — it is less rigorous, because it dresses a guess as a fact.
- **You are not grading the plan.** You are hunting for every place it decides things
  nobody currently knows enough to decide, and for every place a space stayed blank
  only until something was painted into it.

## 2. What the owner explicitly likes (do not throw these out)

- **Deciding each day's work in advance.** A short-horizon commitment of "tomorrow we
  work on X" is wanted. The objection is to *depth*, not *horizon*.
- **"Press Play and see X" every day.** This is a discovery mechanism. Keep it and
  strengthen it: each day should also state **what it expects to learn**.
- **Building incrementally on what the previous day taught.** The cadence must feed
  learnings forward, not execute a frozen script.
- The load-bearing invariants already proven in the real codebase — inventory as sole
  quantity authority, converter-never-counts-workers, explicit employment, the pilot
  lease, publish-then-commit logistics, extraction outside freight arbitration. These
  are *facts*, not guesses. Protect them.

## 3. The core question

For every decision embedded anywhere in the plan, ask exactly one question:

> **"Do we know enough *today* to make this decision — and if not, when will we?"**

Then classify it:

- **A — Forced.** A mechanical consequence of existing code or of the owner's stated
  intent. Keep. (Most of P0-A's staffing split qualifies: the seams it cuts are real,
  in a file that exists.)
- **B — Needed now.** Downstream work is genuinely blocked without it, and no
  experiment could inform it first. Keep, but tag with a **revisit trigger** ("revisit
  when the first corridor exists").
- **C — Decidable later, with more information.** Blank it. Specify *only the seam*:
  the interface, the question to answer, the experiment that answers it, and the
  **last responsible moment**.
- **D — App-shaped bullshit.** Invented because planning AIs fill blank space —
  invented constants, invented schemas, invented UI, invented balance. Delete from
  tickets; record in the decision backlog as an open question.

A decision about a system that does not exist yet may not ship in class C or D without
a trigger condition attached. That is the whole law.

## 4. What to hunt (the categories, with seed examples I already found)

Seed examples are starting points, not the full list. Find more. Cite file + day/ticket
for every finding.

1. **Invented constants.** Numbers that feel like design but are guesses:
   hydration 0 → death at 24h, nutrition 0 → 72h (P0-D, Day 20); homeless restfulness
   `0.5` (P0-D); 15° rotation snapping (Day 15); alert thresholds — food < 12h,
   colonist blocked > 2h, port holding > 1h, site starved > 8h (Day 23); "48h of food"
   starting stock; 8 colonists split 3 Pilots / 2 Farm Techs / 3 Builders.
2. **Invented entities.** Types and machines specified to field level for systems never
   designed: the full `ScenarioDefinition` field list (P0-D, Day 22);
   `PopulationManager.Kill()`'s eight-step unwind order (P0-D); `WorkforceAllocator`
   tick priority 90; the ledger schemas (Day 9); `HabitationComponent` auto-homing
   behavior.
3. **Invented UI.** Panel layouts, column lists, and hotkeys decided before anyone has
   played: HR v1/v2 layout (Days 11, 18), colony-report table columns (Day 23), HUD
   contents (Day 8), Space/1–4/F/Esc hotkeys (Day 25).
4. **Invented content.** `Regolith` as the single construction material; the
   `BuildingDefinition` entry list (Corridor/Habitat/Farm/Water Processor/Dock Port
   Module); the Farm's production curve "0.65 for one tech."
5. **Invented preferences.** Choices presented as settled with no alternative weighed:
   Water Processor automated, hybrid target+allocator staffing, node-snapping placement
   on a 2.5D plane ("Locked on the spot" in DAY_BY_DAY_PLAN.md — the words "locked" and
   "on the spot" appear in the same sentence, which is the disease).
6. **Sequencing lock-in.** Days coupled to upstream guesses: Day 22's bootstrap
   consumes Days 14–17's shapes; Day 23's report consumes Day 9's ledger fields; Day 18
   consumes the split's shape. When an upstream guess dies, how far does the collapse
   propagate? Map it.
7. **Authoritative framing of guesses.** "Locked" designs for unbuilt systems
   (`01_LOCKED_DESIGN.md`), "The 12 binding rules" (`ARCHITECTURE_CONSTITUTION.md`),
   "locked" sections in `GAME_DESIGN_DECISIONS.md`, a `DECISION_LOG.md` of unratified
   decisions. Locking is the mechanism by which a guess becomes load-bearing. Every
   "locked" that encodes a C/D decision is a finding. Note: Fable himself marked two
   packets "DRAFT, lock after Day 13" — credit the instinct, then extend it to
   *everything*.
8. **Deletion of the blank.** Places where the plan filled a space the owner wanted
   blank — e.g., `GAPS_AND_OPEN_QUESTIONS.md` answers some questions that should have
   stayed open, and `HOW_IT_WORKS.md` narrates a day-in-the-life of a game that has
   never been played, in enough detail that it will anchor expectations.

## 5. Method

- Read in this order: `PLANNING_REVIEW_FACTCHECK.md` (what's real) → `HOW_IT_WORKS.md`
  (the proposal) → `GAME_DESIGN_DECISIONS.md` + `ARCHITECTURE_CONSTITUTION.md` (the
  claimed authority) → `DAY_BY_DAY_PLAN.md` (the schedule) → packets in dependency
  order P0-0, P0-A, P0-S, P0-B, P0-P, P0-C, P0-D, P0-E, P1-X.
- Skim the real code as needed to distinguish class A from class D: if a decision is
  forced by something that exists, verify it exists.
- For each ticket and each day, extract **every** decision and classify A/B/C/D.
- For every C/D: name the missing information, the experiment or milestone that
  produces it, the last responsible moment, and the **minimal seam** that keeps the
  plan runnable without the decision (an interface, a placeholder question, a spike
  ticket — never a filled-in design).
- Map the dependency collapse: for each C/D decision, which downstream days consume it,
  and what becomes blank when it does.

## 6. Deliverables

1. **`AUDIT_FINDINGS.md`** — numbered findings, each with: the decision, where it
   lives (file + day/ticket), its class (A/B/C/D), the missing information, the last
   responsible moment, and the replacement (seam, spike, or blank). Ruthless and
   specific; no hedging; every finding names the blank that should exist instead.
2. **A rewritten plan skeleton** (propose as a replacement for `DAY_BY_DAY_PLAN.md`):
   keep the owner's liked structure — commit each day's work one day ahead — but each
   day now carries: (a) the work, (b) the observable, (c) **what it expects to learn**,
   (d) which decision points it feeds. Only ~3–5 days are ever specified at working
   depth; everything beyond exists as a *question* plus a trigger condition, not as a
   design. The horizon may still sketch shapes; the depth may not.
3. **`DECISION_BACKLOG.md`** — every C/D decision extracted from tickets into one
   visible register: the question, the trigger that fires it ("decide when you've
   watched your first commute"), who decides (the human, always), and the current
   blank. Demote the "locked" sections of `GAME_DESIGN_DECISIONS.md` and the
   Constitution's rules to "proposed — trigger pending" where they encode C/D material.
4. **A de-specification pass** on the worst offenders — rewrite the 3–5 most
   over-specified tickets (candidates: P0-D's needs/death/housing, Day 22's
   ScenarioDefinition, Day 23's report, Day 15's placement) so they specify only seams
   and first-questions, with the guesswork moved to the backlog.
5. **A protect-list** — what is genuinely good and load-bearing (the codebase
   invariants, the staffing split's seams, publish-vs-commit, the additive-scene split,
   the daily observable) so the de-specification doesn't throw out real structure.

## 7. Guardrails

- Produce **no new 26-day design**. Produce a rolling cadence with a small committed
  near-term window and a trigger-gated backlog.
- Never mark a finding "wrong" — mark it **VETO-ABLE**. The human vetoes; agents
  propose. Preserve Fable's reasoning trail so vetoes are informed.
- Do not fill any blank you open. If the right seam is unclear, the finding is
  "here is the blank; here is how we will learn what belongs there."
- Respect facts about the real codebase as facts. Class A stays.
- The plan must remain runnable day-by-day: each committed day still ends with
  "Press Play and see." Blankness never becomes an excuse for vagueness about *today*.

## 8. Definition of done

A reader of the revised plan can answer, in one minute each:

1. What are we building in the next three days, and what will we learn?
2. What have we **explicitly refused to decide yet**, and what triggers each decision?
3. Which decisions survive because existing code forces them?

… and the plan contains **zero** decisions about systems that don't exist without a
trigger condition attached. Spaces stay blank until we put the right thing there.
