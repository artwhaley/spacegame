# Replacement Plan Skeleton — Rolling Discovery Cadence

**Purpose:** replace `DAY_BY_DAY_PLAN.md` without replacing Fable's guesses with new guesses.

This keeps the owner's preferred cadence:

- decide each day's work in advance;
- every day ends in **Press Play and see…**;
- every day also says **what we expect to learn**;
- tomorrow's work is allowed to change because of today's observation;
- only the next ~3 days are at ticket depth;
- everything beyond the working horizon is a question with a trigger, not an implementation design.

## Rules for this plan

1. A day may implement an **A** decision, a **B** decision with a revisit trigger, or a **spike** designed to answer a C/D question.
2. A spike may use disposable constants. They are labeled `SPIKE VALUE — NOT DESIGN` and are not copied into canonical docs.
3. An untriggered backlog item cannot appear in an executor ticket.
4. The human is the ratifier. Agent output is `PROPOSED` until the human chooses it.
5. Every day ends with four lines: **Observable / Expected learning / Decisions fed / Tomorrow gate**.

---

# Committed working window

## Day 1 — Baseline ownership + safe parallel-work seam

### Work
- Record the exact current behavioral baseline at `75e5784`: current staffing/transport/extraction ownership, key scene singletons, and one repeatable 24-game-hour observation run.
- If parallel agents are about to touch scene content, perform the additive-scene split as a **process seam**, preserving behavior exactly. Do **not** add future-only socket fields or future UI content.
- Keep clock pace configurable. During the observation, manually compare a few pace settings; do not establish a new canonical default.

### Press Play and see…
The same colony behavior before and after the scene/process seam. No new gameplay.

### Expected learning
- Whether additive loading changes singleton/registry behavior or creates authoring friction.
- Which current scene objects are truly cross-system dependencies.
- What current simulation pace is actually watchable before new walking/flight presentation exists.

### Decisions fed
- Whether the scene split stays.
- What minimum presentation/content prefab convention is really needed.
- Clock-default question (still blank).

### Tomorrow gate
Proceed only if the baseline is reproducible and the scene split, if performed, is behavior-neutral.

---

## Day 2 — Staffing split, part 1: extraction without redesign

### Work
- Run P0-A characterization against the real `StaffingManager`.
- Extract the schedule/report formatting and employment-registry responsibilities behind the existing facade.
- Preserve public API and behavior. Do not add allocator, walking, UI, new events, or “for later” abstractions.

### Press Play and see…
The same authored employments, assignments, schedules and ship-pilot behavior as the baseline.

### Expected learning
- Whether employment really is separable without hidden coupling.
- Which methods are genuine stable seams versus merely nearby code.
- What callers depend on `StaffingManager` behavior rather than its implementation.

### Decisions fed
- Exact shape of the remaining staffing split.
- Where a future commute experiment can attach without adding a second employment authority.

### Tomorrow gate
Proceed only if assignment results and observed duty behavior are unchanged.

---

## Day 3 — Staffing split, part 2: isolate commute/reconciliation ownership

### Work
- Extract commute batching, colonist reconciliation and pilot-duty reconciliation from the facade with zero intended behavior change.
- Keep one duty-state writer and one explicit employment path.
- Record the actual current points where a colonist commute is requested and where arrival is committed.

### Press Play and see…
The same 24–48 game-hour staffing/commute/crew behavior as the baseline, with the facade materially smaller and responsibilities explicit.

### Expected learning
- Whether the proposed responsibility boundaries survive real behavior.
- Exactly where a walking mode can be introduced without inventing parallel state.
- Which current movement assumptions are ship-specific and which are general transit facts.

### Decisions fed
- First walking spike.
- Whether any route-resolver abstraction is needed at all yet.

### Tomorrow gate
Only promote the walking spike if the split is stable and the insertion seam is obvious from the code that now exists.

---

# Candidate Day 4 — promoted only after Day 3 review

## One-commute walking spike

### Work
- Create the smallest reversible experiment that lets **one authored adjacent commute** occur as walking instead of a passenger contract.
- Preserve `currentLocation = last arrived` and separate in-transit state.
- Do not add general Dijkstra routing, construction corridor schema, multi-plane support, future pressurization, or canonical traversal constants.
- A scene-authored spike value may control duration; label it disposable.

### Press Play and see…
A Farm worker visibly/logically leaves one anchor, is in transit, and commits arrival only when the walk completes; switching the spike off returns to current ship commute behavior.

### Expected learning
- Whether walking is visually legible and strategically different enough to deserve first-class treatment.
- What information presentation needs from transit.
- What commute duration feels readable at the actual clock pace.
- Whether a general graph is needed now or later.

### Decisions fed
- Routing abstraction.
- Traversal-time ownership.
- Walking presentation.
- Corridor meaning.

### Tomorrow gate
Human reviews the commute. Do not generalize the spike before that review.

---

# Beyond the working window — questions only

These are **not tickets**. They become ticketable only when their trigger fires.

| Question | Trigger that promotes it | First experiment / evidence required |
|---|---|---|
| What should ship travel look and feel like? | One current voyage can be watched at a useful pace and its current shortcomings can be named | Compare the current mover plus presentation against one bounded alternative; human watches both |
| Do stations need finite docking capacity, and how should contention resolve? | Two real ships create observable contention | Minimal one-berth contention spike; record failure modes before choosing queue semantics |
| What is the UI technology? | First management task is ready to leave the Inspector | Build the same tiny management surface in candidate workflows; human chooses based on authoring + result |
| What belongs on the HUD? | A play session repeatedly requires information not visible in world/panels | Record the questions the human asks during play; expose only those |
| Which reports/ledgers exist? | A real player/debug question cannot be answered safely from current state/history | Add exactly one read model that answers that question |
| How are buildings placed? | Two or three representative module placeholders exist | Placement sandbox; human tries alternatives; no production API until choice |
| What construction material(s) exist? | First construction experiment needs something hauled | Use disposable test resource, then human chooses content after observing the loop |
| How does a construction site work? | Placement approach is ratified and one placed site exists | Try reuse of inventory + logistics + staffing; observe hauling/building timing before schema lock |
| Is staffing automation needed? | Human has manually staffed several real shifts/facilities through usable UI | Record repetitive/painful actions; choose automation model only then |
| What happens when food/water is unavailable? | Economy can intentionally create a shortage and the shortage is observable | First expose unmet consumption with no lethal constants; human chooses consequences |
| How does housing work? | Sleeping/home behavior is visible and a shortage can be created | Observe what “homeless” should mean in this game before creating penalties/automation |
| What is a scenario asset? | A start can be assembled manually through stable production paths at least twice | List the authored facts actually required; that list becomes the candidate schema |
| What should the colony report/alerts say? | Two sessions produce missed or late information | Turn observed information failures into one report/alert at a time |
| What is the environment production workflow? | Camera, module scale and core scene composition are stable enough to judge look-dev | One small visual spike against references; decide generator/manual/purchased path afterward |
| What is Phase 1 exploration? | Phase 0 playable exists and its seams have survived use | Re-open P1-X from the now-real architecture; no Phase-0 future-proofing solely for the old proposal |

---

# Daily template going forward

```md
## Day N — <short outcome>

### Work
<Only the work committed for this day.>

### Press Play and see…
<One observable human-reviewable behavior.>

### Expected learning
- <What uncertainty this work should reduce.>

### Decisions fed
- <Backlog decision IDs/questions this evidence informs.>

### Revisit triggers
- <Any B-class temporary choice made today and when it must be reconsidered.>

### Tomorrow gate
<Condition for promoting the next candidate day.>
```

The absence of a Day 12 implementation spec is intentional. The plan earns Day 12 by learning from Days 1–11; it does not pre-spend those learnings.
