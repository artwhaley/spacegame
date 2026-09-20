# Synthesis — Blank-Space Audit + Revised Direction

**Repository:** `artwhaley/spacegame`  
**Planning baseline reviewed:** `75e5784440f6f800c2e16241a4b59d5e500ba613`  
**Inputs synthesized:** OpenAI audit, DeepSeek audit (already incorporating Muse/GLM), Claude audit, and the owner's new direction for human avatars / NavMesh facilities / interactable work cycles / docking.

## Executive conclusion

All three audits agree on the important diagnosis:

> Fable's plan contains a strong code-grounded spine, then spends too much certainty on systems that have not been built, watched, or played.

The repair is **not** to throw away the plan. It is to preserve the proven ownership seams and owner-stated design choices while turning unearned detail back into questions, placeholders, and experiments.

The owner's new information changes the near-term sequencing materially. A parallel prototype already demonstrates an **interactable facility** that can direct a worker through a local visual work cycle. That is a much better early discovery tool than another week of infrastructure. The first three days should therefore create one end-to-end visible loop:

1. a real human avatar exists on a navigable facility surface;
2. the Farm visibly directs that avatar through a work cycle; and
3. a shuttle docks at a physical berth and people visibly board/disembark at the right moments.

That loop teaches us more about the final game than a speculative UI/report/construction schema can.

## What the other audits strengthen

### 1. Reversal cost is the missing prioritization axis

Claude's audit adds an important distinction: not every premature choice is equally dangerous.

- A number in a content asset is cheap to change.
- A UI layout is annoying but reversible.
- A schema or authority consumed by six later tickets is expensive.
- A choice that silently eliminates an open design question is the most dangerous.

This changes the emphasis of the audit. The highest-priority removals are not every guessed constant; they are **premature authorities and schemas**: per-colonist needs, `ScenarioDefinition`, the full `ModuleSockets` contract, the allocator algorithm, reporting schemas, and placement/world abstractions.

### 2. The needs plan contains a hidden model rewrite

DeepSeek and Claude both identify a more serious issue than my original "death timers are guessed" finding.

The existing `PopulationResourceConsumer` is aggregate-at-a-habitation. It does not know *which* person ate or drank. Fable's proposed `Fed(colonist, resource, fraction)` silently changes scarcity from a colony/place fact into a per-person allocation model.

That forces questions the plan never asked:

- If there is half enough food, who is hungry?
- Does everyone receive an equal fraction?
- Can one colonist be hungry while another is not?
- Is shortage a colony pressure or an individual simulation?

This is now a **top-tier backlog decision**. The first survival experiment should surface the existing aggregate shortage and watch whether that is enough. Personal needs come only if the game proves it needs them.

### 3. `ModuleSockets` is not just over-specified; it can steal authority

My original audit flagged the eight-field socket list as future systems compressed into one schema. The other reviews sharpen this: making `HabitationComponent.capacity = beds.Count` would let presentation transforms own an economy fact.

That is backwards. Runtime bed capacity must remain runtime authority unless the owner deliberately decides that physical bunk geometry *is* capacity. Visual bed points may validate against capacity; they should not silently define it.

The revised near-term convention therefore grows **only when an immediate consumer appears**. Days 1–3 may earn a mesh/avatar root, docking berth/approach points, and facility interaction points. Beds, corridor nodes, holding slots, EVA points, etc. arrive only with the feature that consumes them.

### 4. Several contradictions are real defects, not design disagreements

The other reviews caught concrete cross-document inconsistencies that my first pass did not emphasize enough:

- The duplicated Constitution in `ROADMAP.md` differs from the canonical file and names `ShipPhase`, which is not the actual type.
- The starting Command Pod is described as both 6 beds and 8 beds.
- The camera proposal hard-codes a `y = 0` pivot while the placement plan says never hard-code `y = 0`.
- `GAPS_AND_OPEN_QUESTIONS.md` contains recommendations that effectively answer questions declared open elsewhere.
- P0-C correctly marks the construction-site access question **open**, then `DAY_BY_DAY_PLAN.md` silently promotes the recommendation into fact.
- The proposed holding-slot modulo rule allows multiple queued ships to occupy the same holding pose.
- Moving visible geometry off the simulation root can leave `LocationAnchor`'s auto-added collider as a 1×1×1 box, which later breaks selection even though Day 2 appears to work.

These belong in the patch set as cleanup items, not merely backlog questions.

### 5. The manual staffing UI has an earned core

The reviews correctly distinguish the **data that already exists** from the invented screen.

`GetAssignmentCandidates` and `AssignmentResult` already provide a strong user-facing seam: select a job/shift and show every candidate with the exact reason they can or cannot take it.

Keep that. Do not lock the two-pane HR layout, filters, badges, or modal behavior until the player has actually managed a shift.

### 6. The scenario problem is "derive, don't predict"

The old plan predefines a large `ScenarioDefinition` schema using shapes that do not yet exist. The better principle is:

> When New Game is finally built, derive the minimum data representation from the slice that actually exists then.

A scene-capture/round-trip tool is one plausible implementation, but it is **not** itself a new lock. The requirement is simply that scenario/bootstrap shape be derived from real objects and round-tripped, rather than predicted weeks ahead.

## What changes from my first audit

### Reclassified: Farm `0.65` is not invented

The original audit prompt itself contains a false-positive seed: the Farm's one-worker `0.65` production curve is already shipped content. It is a fact about the current project, not Fable filling a blank.

The same distinction applies to `HabitationComponent.restfulnessMultiplier`: the **seam** exists. The proposed homeless value `0.5` is the guess.

### Strengthened: hybrid staffing is owner-stated; the allocator algorithm is not

The target/priority/pin/manual-control concept is recorded as an owner choice. That survives.

What does **not** survive is the invented autonomous algorithm: preemption, LIFO release, hourly throttle, four-hour hysteresis, etc. First expose targets and let the human fill them manually. Only automate the part that becomes tedious in play.

### Strengthened: one voyage authority is a good consolidation seam

Three components currently hand-roll ship movement. Consolidating ownership of a voyage is earned work.

But that does **not** force a custom Newtonian 6DOF implementation. A deterministic kinematic voyage can satisfy the same authority and pause/speed requirements. Flight feel is an experiment, not an architecture theorem.

### Changed: the infrastructure-first opening is replaced

My first rewrite started with planning/scene/staffing cleanup. The owner's new interactable-facility prototype changes that.

For the next three days, we explicitly accept a short period of working inside the current scene because the observable payoff is much larger:

- human avatar,
- navigable facility,
- visible work cycle,
- physical docking,
- visible boarding/disembarking.

After that visible loop is proven, we do the scene split and staffing refactor before adding more simulation writers.

## What the other audits caught that I did not

The following are being added to the revised packet:

1. **Aggregate vs personal scarcity is an architectural fork**, not plumbing.
2. **No-home behavior currently preserves 1.0 restfulness**; `0.5` is a behavior change, not preservation.
3. **Some death cleanup already occurs in existing lifecycle methods**, so `Kill()` acceptance must prove the things that *do not* self-clean rather than merely checking registries.
4. **Bed socket count must not become runtime capacity authority by accident.**
5. **The duplicated Constitution is already inconsistent.**
6. **The 6-vs-8 bed count drift proves guessed content has already rotted inside one planning revision.**
7. **Camera `y=0` vs placement "never y=0" is an internal contradiction.**
8. **A holding-slot modulo rule produces overlapping ships for queue depth > authored slots.**
9. **The collider regression created by moving meshes under a child root is a real near-term implementation hazard.**
10. **`ActiveWorkersChanged` / reporting event/tick behavior needs one clear update mechanism to avoid double-counting or duplicate transitions.**
11. **The old plan tunes the economy before survival pressure exists, then tunes again.**
12. **The glossary and future roadmaps can give nonexistent types the same authority as shipped ones.**

## Suggestions I reject or modify

### Reject: choose 6 beds because the old Day 21 demo needs a shortage

That is circular. A speculative observable cannot justify the number that makes the observable true.

The revised plan leaves starting bed count as current content until housing becomes the experiment. If we deliberately create a shortage for that experiment, it is test setup, not canon.

### Reject: classify custom Newtonian 6DOF as forced

Determinism, pause safety, and one ship-motion authority are real constraints. A home-grown Newtonian integrator is only one solution.

The revised plan first puts docking and voyage ownership around the movement model already present. Only after watching it do we decide whether kinematic motion is insufficient and what replacement is warranted.

### Reject: replace one line-count magic number with another

`StaffingManager` is unquestionably too broad. But "≤250" or "no file >300" should not be a behavior gate.

The refactor is complete when responsibilities are isolated, the facade remains stable, and behavior is unchanged. Line count is a smell/report, not a pass/fail requirement.

### Reject: Constitution rules 1–11 are all mechanically forced

Several are strongly supported by current code: one authority, narrow commands, tick-only runtime progression, view read-only behavior, registry discipline, and facility-performance channels.

Others are architectural preferences or process policies: reusable-by-two facility rule, line ceilings, serialize-every-authoritative-field-for-future-save, and mandatory per-ticket tests. They may be good, but should be labeled as policies rather than facts.

### Modify: "capture the scene into ScenarioDefinition" is a principle, not a required API

The valuable insight is to derive scenario shape from the real slice and prove a round-trip.

The exact mechanism—Editor capture command, hand-authored minimal asset, or another representation—stays open until that day.

### Modify: UI Toolkit spike

The old plan's global UI Toolkit lock is removed. We also do **not** spend Day 3 on a UI technology spike now; Day 3 has a higher-value docking/boarding experiment.

When the first real management panel arrives, choose the fastest working UI path in the actual project. Do not turn that implementation choice into a project-wide constitution until repeated use justifies it.

## New owner direction that now becomes protected intent

These are not AI guesses; they are the new planning inputs:

- Colonists should quickly stop looking like capsules and become human avatars.
- Facilities should stop being inert blocks and have navigable interior/work surfaces.
- A facility can direct an arrived worker through a visible local work cycle such as console → crop task → crop task → repeat.
- The first integration should reuse the parallel interactable-facility prototype rather than reinventing it from prose.
- Shuttle docking should become physical early: a ship parks at a berth and colonists board/disembark at the correct time.
- Seeing people physically do the work is a core discovery mechanism, not polish to defer until the simulation is "done."

One boundary is deliberately explicit for the first integration:

> **The work-cycle layer visualizes what an active worker does locally. It does not become a second authority for employment, staffing eligibility, facility productivity, recipes, or resource quantities.**

That boundary can be revisited later if interaction tasks become gameplay, but it prevents the prototype from accidentally creating a second simulation.

## Final synthesis

The revised project shape is:

1. **Make the existing simulation legible in the world.**
2. **Use what becomes visible to discover the right movement/docking/facility seams.**
3. **Then perform the known behavior-preserving refactors before adding more writers.**
4. **Only build UI, construction, survival, and session structure in response to questions that actual play raises.**
5. Keep a 28-day human roadmap for orientation, but only the next three days are working-depth commitments.

The plan is allowed to know where it is going. It is not allowed to pretend it knows the exact road.
