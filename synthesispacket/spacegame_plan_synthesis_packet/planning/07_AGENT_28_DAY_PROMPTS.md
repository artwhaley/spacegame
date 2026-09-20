# 28-Day Agent Prompt Plan

These are **starting prompts**, not frozen ticket contracts. Each assumes the agent has local repository access and can run/inspect Unity project files.

## Universal preamble for every day

Before editing:

1. Read the repository's `START_HERE.md`.
2. Read the active `DAY_BY_DAY_PLAN.md` / 28-day plan, `DECISION_BACKLOG.md`, `PROTECT_LIST.md`, and planning-governance document.
3. Read yesterday's learning report if one exists.
4. Inspect the actual code/content at HEAD. Do not trust a stale planning claim over current source.
5. Run `git status`; do not discard unrelated local work.
6. Treat future-day descriptions as orientation, not API specification.
7. Preserve code-grounded invariants and owner decisions.
8. For any new choice, label it FACT / OWNER DECISION / WORKING HYPOTHESIS / OPEN QUESTION in your final report.
9. End with the requested **Press Play and see** observable and a `Day N Learnings` note.
10. If the observable fails, do not silently proceed into tomorrow's scope.

The prompts below deliberately repeat the critical guardrails so they can be copied independently.



---

## Day 1 Prompt — Human avatars + navigable facility blockout

```text
Read `START_HERE.md`, the active 28-day plan, planning governance, decision backlog, protect list, and the current code before editing. Today is a presentation-first experiment.

Goal: replace one capsule colonist's visual body with an existing human avatar while preserving the colonist simulation object and all authoritative state. Make the Command Post and Farm simple NavMesh-ready/navigable blockouts. Prove the avatar can move locally inside the Farm without changing `ColonistAgent.currentLocation`.

Guardrails:
- Do not move employment, fatigue, duty state, logical location, recipes, or inventory into the avatar/presentation layer.
- Prefer a visual/presentation child for Animator/NavMesh behavior; do not let frame-time movement become simulation truth.
- If you move mesh geometry off a `LocationAnchor` root, explicitly inspect/fix collider bounds; the auto-added root collider must not silently become a 1x1x1 box.
- Add only anchor/socket concepts consumed today.
- Do not implement corridor routing, a universal animation state machine, UI, final art, or future facility interactions.

Acceptance: Press Play and show a human avatar in the Farm, moving between two local points on navigable geometry, while the simulation still reports the colonist at the Farm and the existing colony behavior continues.

End with a short `Day 1 Learnings` report: files changed, observable, confirmed/falsified assumptions, new questions, and any changes recommended for Days 2–3.
```


---

## Day 2 Prompt — Port the interactable Farm work cycle

```text
Read the active planning docs and inspect the real parallel interactable-facility prototype before designing anything. If the prototype source is not inside this repository, use the local source path supplied to the session or inspect obvious sibling project directories. If the source cannot be found, do not recreate it from this prompt; stop implementation after documenting exactly what source path is needed.

Goal: port the minimum reusable pieces needed for one Farm worker to perform a visible local work cycle after the simulation already considers that worker active at the Farm.

Use the prototype's existing shape as evidence. A representative cycle is console/sit -> crop task -> another crop task -> repeat, but timing and exact step names are prototype content, not canon.

Guardrails:
- Runtime staffing decides whether the colonist is an active worker.
- Existing recipe/inventory/facility-performance code remains the only production authority.
- The facility/work-cycle layer may direct local NavMesh movement, poses, and animation only.
- Do not invent a universal action graph for all future facilities.
- Make cancellation/interruption truthful when duty ends or the worker is no longer active.

Acceptance: Press Play and watch an active Farm worker navigate between real interaction points and repeat the imported work cycle; remove/end their duty and watch the cycle stop cleanly.

Report the actual seam discovered: what data the facility provides, what the avatar/view consumes, what owns cycle state, and which questions remain open.
```


---

## Day 3 Prompt — Physical shuttle docking + visible boarding/disembarking

```text
Read the current ship, transport-contract, passenger-carrier, pilot-lease, and movement code before editing. Today is not a flight-model rewrite.

Goal: make one shuttle physically park at a real berth and make human passengers visibly board and disembark at the correct boundary.

Work from the movement system that already exists. Add only docking data consumed immediately: berth/capture pose, approach point only if needed, and passenger entry/exit point. Preserve existing transport-contract and responsible-pilot authority.

Guardrails:
- No custom 6DOF/PhysX decision today.
- No final berth queue or priority policy.
- Do not let cargo or passengers visually unload before the ship is actually parked.
- Do not replace `PassengerCarrierComponent` truth with an animation flag.
- Record awkward intermediate states instead of adding speculative fallbacks.

Acceptance: Press Play and watch a human walk to the shuttle, board, the shuttle depart, arrive/park at the destination, and the human visibly exit and proceed into the facility. Existing contract completion remains correct.

Report which docking/boarding states were genuinely required and what Day 10 contention experiment should test.
```


---

## Day 4 Prompt — Scene/presentation cleanup based on real consumers

```text
Use Days 1–3 as the evidence. Do not revert to the old speculative eight-field `ModuleSockets` design.

Goal: clean up scene/assembly/prefab ownership so the visible human-work-shuttle loop is safe to iterate on and parallel agents can work without constantly colliding.

Make only boundaries the current implementation now justifies: simulation/managers, base/content, presentation/environment, UI only if actually present. Runtime must not depend on Presentation. Consolidate only the anchor/socket fields that Days 1–3 really consume.

Guardrails:
- Preserve the complete Day-3 observable.
- No beds/corridor nodes/holding slots/EVA points just because old plans mention them.
- Fix broken cross-scene references with registries/lookup rather than new global caches.
- Verify root colliders, NavMesh links/surfaces, Animator references, and docking transforms survive prefab/scene changes.

Acceptance: launch from the new/cleaned scene arrangement and reproduce the same human work + shuttle boarding/docking loop.

End with a dependency map of actual cross-scene/runtime-presentation seams found.
```


---

## Day 5 Prompt — StaffingManager split, employment/reporting first

```text
Read the current `StaffingManager` and characterize behavior before cutting. Preserve the public facade and all current assignment results.

Goal: extract responsibilities that can move with zero behavior change, beginning with schedule/report formatting and explicit employment/assignment logic.

Guardrails:
- This is a move, not a redesign.
- No allocator, no new staffing event for a future screen, no walking-route logic.
- Keep employment mutation through the existing validated assignment API.
- Do not use a hard line-count target as acceptance; report line counts only as a smell.
- Preserve or add focused regression characterization where it actually protects behavior.

Acceptance: the same Day-4 colony runs, manual assignment results/reasons are identical, and the visible worker/docking loop is unchanged.

Report moved methods, unchanged public API, behavior proof, and any responsibility boundary that differed from the old P0-A sketch.
```


---

## Day 6 Prompt — StaffingManager split, commute/pilot/colonist reconciliation

```text
Continue the behavior-preserving staffing split from Day 5.

Goal: isolate commute batching, pilot-duty reconciliation, and colonist reconciliation behind the existing `StaffingManager` facade while preserving sole ownership of duty-state writes and current pilot handoff behavior.

Guardrails:
- Do not add the new walk/shuttle resolver yet.
- Do not change assignment rules, shift semantics, fatigue tuning, or ship pilot lease semantics.
- Keep the visible Farm work-cycle and boarding sequence as regression observables.
- Prefer clear responsibility over arbitrary file-size gates.

Acceptance: run a full shift change including Farm work, pilot handoff, shuttle transport, and return. Behavior matches the pre-split baseline.

Report the exact method/seam where Day 7 should ask 'walk, ship, or unreachable?'.
```


---

## Day 7 Prompt — Minimal walk-vs-shuttle route decision

```text
The owner has already chosen the strategic rule: corridor-connected commutes walk; otherwise they use shuttle transport; if neither is possible, the colonist is visibly blocked.

Goal: implement the smallest route-selection seam that proves that rule using one authored Command Post <-> Farm walk connection.

Guardrails:
- Do not require Dijkstra unless today's graph actually has multiple competing paths.
- No construction placement, corridor damage, pressurization, capacity, or universal graph features.
- Logical transit/arrival stays simulation-owned; NavMesh/avatar movement is presentation.
- Walking duration is an explicit placeholder dial to tune by watching a real avatar.
- Busy shuttle means waiting, not 'unreachable', if the existing contract system can queue work.

Acceptance: Farm worker walks; worker to the shuttle-served facility flies; disable/remove the one walk connection and observe a truthful mode change/block.

Report what route information the simulation actually needed and what remained purely presentation.
```


---

## Day 8 Prompt — Make the walking commute visually truthful

```text
Use Day 7's logical Walk result and Days 1–2's NavMesh/avatar layer.

Goal: show one complete home -> corridor -> Farm -> interactable work-cycle commute with body movement synchronized to logical transit.

Do not assume the old plan's `LegProgress01` solution is correct. Test the handoff. Decide only what today's observable forces: whether sim ETA drives the body, body arrival gates sim commit, or a narrow hybrid handshake is needed.

Guardrails:
- `currentLocation` must not claim arrival before the chosen truthful boundary.
- Avoid per-frame writes to unrelated simulation state.
- Do not generalize to arbitrary path networks beyond the test corridor.

Acceptance: watch a worker leave home, physically traverse to the Farm, become logically arrived at the agreed boundary, then start the work cycle without teleport/pop.

Report the synchronization rule as a FACT/HYPOTHESIS and add any unresolved movement question to the backlog.
```


---

## Day 9 Prompt — One ship voyage authority, preserve current motion

```text
Read all current callers that hand-roll ship travel/docking. Goal: consolidate movement ownership so transport, extraction, and crew-return request a voyage through one authority, while preserving the movement feel that already works.

Guardrails:
- This is an authority/refactor day, not a physics day.
- Do not introduce a Newtonian integrator, collision, fuel, or final queue policy.
- Preserve pause/speed behavior, responsible-pilot lease, contract ownership, and Day-3 physical docking/boarding.
- Let actual required phases emerge from current docking/boarding rather than copying the old proposed phase enum blindly.

Acceptance: transport, extraction, and crew-return all use the same voyage owner and the visible shuttle sequence still works.

Report duplicated writers removed and any phase that turned out unnecessary or newly necessary.
```


---

## Day 10 Prompt — Dock contention experiment before queue design

```text
Create a controlled case where two ships want the same berth.

Goal: observe the real failure and implement only the minimum berth reservation/holding behavior required to prevent physical overlap, stolen berths, or deadlock.

Guardrails:
- Do not copy the old priority/FIFO/modulo-holding-slot policy unless the observation requires it.
- Never place multiple queued ships at the same holding pose.
- A busy berth is a wait state, not a teleport or silent success.
- Keep queue policy/tuning in the decision backlog if one simple first-come reservation is enough for today.

Acceptance: two ships contend for one berth; both obligations remain intact; only one parks; the other waits somewhere truthful and eventually proceeds.

Report what the player can actually see/understand about the wait and what queue policy question remains.
```


---

## Day 11 Prompt — First real facility management panel

```text
Build one management surface because there is now something worth managing.

Goal: selecting the Farm should answer the immediate questions raised by the visible slice: production state/blocker, relevant inventory, assigned/active workers, and current visible work-cycle activity.

Start with the fastest UI technology that demonstrably works in this project. Do not declare the technology a repository-wide law today.

Guardrails:
- UI reads authoritative state; it does not recompute production/staffing truth.
- No global HUD spec, no full panel taxonomy, no styling framework beyond what this panel needs.
- Prefer plain useful text over invented graphs/tables.

Acceptance: click/select the Farm and understand why it is or is not working without opening the Inspector.

Report the UI stack used, why it was the fastest today, and whether there is enough evidence to standardize it yet (probably not).
```


---

## Day 12 Prompt — Manual HR using existing assignment reasons

```text
Goal: make one honest staffing workflow usable without inventing the final HR information architecture.

Expose a selected workplace/role/shift, every candidate, and the exact existing `AssignmentResult`/reason. Support Assign/Unassign through the existing commands.

Guardrails:
- Do not lock job-first/person-first/target-first as the final mental model.
- No allocator, target automation, filters, badges, or speculative columns.
- Keep rejection reasons; they are existing simulation knowledge worth surfacing.

Acceptance: reassign a Farm worker and watch the resulting physical commute and facility work cycle change.

Report which way you naturally wanted to navigate the UI while using it; that observation feeds the HR-model backlog question.
```


---

## Day 13 Prompt — Raw observability feeds, no dashboard schema

```text
Goal: make runtime facts available to future UI without pre-deciding the report.

Add the minimum in-memory transition/event feed and raw resource-change/block-duration history needed to inspect what happened over game time.

Guardrails:
- Store raw events/deltas/intervals; do not pre-author `In24h`, `HoursUntilEmpty`, sparkline buckets, or report sentences unless an immediate caller needs one.
- Ensure event-driven and tick-driven bookkeeping cannot double-count the same transition.
- Read models/diagnostics do not mutate simulation authority.

Acceptance: trigger a resource change, one blocked state, and one ship wait; inspect a clean chronological record in Play Mode.

Report event noise/gaps and the first real diagnostic question that should drive Day 14.
```


---

## Day 14 Prompt — Answer one actual 'why isn't this working?' question

```text
Review the last 13 days' learnings. Pick the single diagnostic question that repeatedly cost time in Play Mode.

Goal: answer that question in-game with the smallest useful UI/explanation. Examples could be facility blocked reason, ship wait reason, or staffing rejection, but choose from observed pain, not this prompt.

Guardrails:
- One question, one useful answer.
- Reuse authoritative state/history.
- No colony dashboard, alert threshold suite, or generic reporting platform.

Acceptance: recreate the failure and get a useful explanation without the Inspector.

Report the question, why it was selected, and whether raw state or event history was the better source.
```


---

## Day 15 Prompt — One placement ghost, minimal rules

```text
Goal: test the physical interaction of placing one building with the real camera and current base.

Create one explicitly temporary/test building definition, a move/rotate ghost, overlap validation, and confirm-to-create-site-marker.

Guardrails:
- Free placement + overlap is the default experiment unless prior play has already earned snapping.
- No 15-degree law, SitePlane IDs, full result enum, unlock/category fields, full build menu, or corridor rules.
- The temporary building definition is test scaffolding, not canonical content.

Acceptance: drag, rotate, reject overlap, place a site.

Report what felt wrong/right about free placement and whether any snap behavior is now justified.
```


---

## Day 16 Prompt — Construction site = delivery + labor + visible work

```text
Goal: prove construction can reuse existing inventory/logistics/staffing and the interactable-work presentation.

Use one explicitly non-canonical temporary construction material. The site requests it, receives it, offers Builder work, and completes one test building when the minimum conditions are met.

Guardrails:
- Do not name the temporary material as final economy canon.
- Reuse facility performance; do not let the builder animation itself add construction progress unless deliberately wired through the existing performance path.
- Do not invent refund/demolition behavior.
- Do not solve isolated-site access unless today's site is actually isolated.

Acceptance: material is hauled, a builder physically works at the site, completion replaces the site with the test building.

Report which construction facts are genuinely reusable and which content values remain placeholders.
```


---

## Day 17 Prompt — Build one corridor and let it change commute mode

```text
Goal: add the smallest player construction interaction needed to create a permanent walk connection between two existing endpoints.

Use Day 15's placement lessons rather than the old corridor rule list. On completion, create the walk link the route resolver already understands.

Guardrails:
- Only rules needed for this first corridor.
- No multi-plane system, max-length doctrine, intersection suite, or full node-snapping system unless the actual interaction demands it.
- Removing/closing the corridor must not teleport anyone.

Acceptance: connect a previously shuttle-served workplace; next commute walks. Remove/disable the link; next commute changes mode truthfully.

Report what corridor-placement model the hands-on test suggests.
```


---

## Day 18 Prompt — Staffing targets as visible data, no allocator

```text
The owner has chosen a hybrid control concept. Today implements only the non-autonomous part.

Goal: add target headcount per role/shift plus the minimum priority/pin data needed by the chosen control concept, and show assigned/target in the staffing UI.

Guardrails:
- No auto-fill/release.
- Do not conflate existing commute priority with work priority; name/own each fact clearly or defer work priority if it is not yet needed.
- No hysteresis/preemption/release-order policy.

Acceptance: set a Farm target to 2, see 1/2, manually assign a second worker, see 2/2 and the physical work loop update.

Report whether manual target filling felt useful, tedious, or confusing.
```


---

## Day 19 Prompt — Trigger-gated staffing automation

```text
Start by reading Day 18's learning report.

If the owner/playtest actually wanted automatic filling, implement the smallest allocator that fills an under-target role through the existing assignment API. Do not automatically move an already-employed worker unless explicitly approved today.

If Day 18 did not create that desire, do not build an allocator just because this is Day 19; instead fix the largest observed staffing-management problem.

Guardrails for allocator branch:
- no invented four-hour hysteresis;
- no LIFO release;
- no hidden preemption;
- use existing assignment validation and return reasons;
- record every autonomous assignment visibly.

Acceptance: the observed Day-18 pain is reduced without surprising churn.

Report which automation policy questions now have real evidence.
```


---

## Day 20 Prompt — Make existing aggregate shortage visible

```text
Read the current `PopulationResourceConsumer` carefully. Do not add per-colonist nutrition/hydration today.

Goal: surface transitions in the existing aggregate consumption shortage state so a food shortage is obvious in UI/history and linked to the location/resource.

Guardrails:
- No `Fed(colonist, ...)` API.
- No ration allocation model, personal need bars, work penalties, or death timer.
- Emit transitions, not per-tick spam.

Acceptance: disable the Farm/food source; when the draw fails, the game clearly communicates the Food shortage and when it clears.

Report whether aggregate shortage was enough to make the failure understandable and consequential.
```


---

## Day 21 Prompt — Make housing visible, consequence blank

```text
Goal: make homes/beds legible and physically represented without inventing the homelessness penalty.

Keep runtime habitation capacity authoritative. Use bed interaction points to animate sleepers up to available authored visual slots; mismatches should be authoring diagnostics, not economy mutations.

Create a deliberate housing shortage as test setup, not the canonical starting scenario. Add manual home assignment only if needed to run the experiment.

Guardrails:
- no hard-coded 0.5 restfulness;
- no auto-homing;
- do not resolve 6-vs-8 starting beds as canon.

Acceptance: one colonist is visibly/unambiguously unhoused; add/assign housing and see the condition resolve.

Report what consequence, if any, the owner now wants after watching it.
```


---

## Day 22 Prompt — Manual death teardown discovery

```text
Goal: add a development/debug `Kill` command that cleanly releases a colonist from all obligations and reports anything it could not unwind.

Use the actual APIs/lifecycle already present. Do not hard-code an eight-step choreography before testing. Specifically distinguish cleanup that already occurs on destroy/unregister from state that would strand references (passenger manifests, pilot lease, transit, employment/duty, etc.).

Guardrails:
- no automatic starvation caller;
- no death threshold;
- command should be robust if one release step fails and should report failures.

Acceptance: kill a colonist in at least two difficult states (e.g. walking/passenger/pilot/working) and verify no live obligation points to the destroyed identity.

Report the teardown order that became fact through testing.
```


---

## Day 23 Prompt — New Game from the slice that actually exists

```text
Goal: make the current playable slice reproducible from data without pre-designing a future schema.

Inspect the actual current object graph and choose the smallest representation that can round-trip the slice. An Editor capture command is an option, not a requirement. The proof is recreate/round-trip, not field-list completeness.

Guardrails:
- do not include fields solely for future multi-site/exploration/research ideas;
- do not mirror authoritative runtime state redundantly;
- starting numbers remain tunable content, not planning-document laws.

Acceptance: start from an empty base state, load/create the current scenario representation, and reproduce the same functional colony.

Report the minimum data that proved necessary and any state that resisted round-trip.
```


---

## Day 24 Prompt — Long run + evidence-based tuning

```text
Run the current integrated slice long enough at accelerated speed to observe production, consumption, commuting, shuttle congestion, construction throughput, staffing gaps, and housing/shortage behavior that now exists.

Goal: gather measurements and tune only values that prevent meaningful observation or create obvious degeneracy.

Guardrails:
- every changed number is recorded as a dial with before/after and reason;
- do not tune toward a pre-written '72h stable' victory condition unless the owner chooses it;
- do not set starting crew/food/ice from old plan arithmetic without measurement.

Acceptance: the slice runs long enough to form clear opinions without permanent deadlock caused by an obvious placeholder.

Produce a metrics/observations note and explicitly identify which backlog triggers fired.
```


---

## Day 25 Prompt — Build only the report/alerts Day 24 demanded

```text
Read Day 24's observation note before coding.

Goal: implement one or two diagnostics/alerts that would have materially improved the long run. Choose the questions from evidence.

If thresholds are needed, derive initial values from observed normal/failure distributions and label them tuning values.

Guardrails:
- no five-section colony report by default;
- no 24-bucket sparkline unless it answers the selected question;
- distinguish 'unknown' from safe/infinite estimates.

Acceptance: reproduce the relevant failure and receive the information you wished you had during Day 24.

Report whether any larger report is now justified.
```


---

## Day 26 Prompt — Cramped-interior presentation/camera experiment

```text
Now use the actual human commute/work/sleep/docking loop to decide how cramped interiors should be viewed.

Goal: implement the smallest camera/visibility treatment that lets the player follow a person through the real module blockouts: cutaway roof, selected-module hide, windowed shell, x-ray, or another approach chosen by hands-on comparison.

Also make only the environment/lighting improvements needed around this active slice.

Guardrails:
- no full procedural environment pipeline unless manual work has become a demonstrated bottleneck;
- no final art bible constants as architecture;
- preserve selection/collider/NavMesh behavior.

Acceptance: follow one colonist home -> commute -> work -> shuttle -> destination without losing the visual story.

Report the chosen working presentation hypothesis and why, plus alternatives still open.
```


---

## Day 27 Prompt — Thin session frame + unaided playtest

```text
Goal: make the current slice enterable by another human with minimal scaffolding and then observe them.

Add only start/restart, pause/speed if needed, and menu/quit behavior necessary for the playtest. Do not invent a population-zero Game Over unless failure meaning has been explicitly decided.

Have another person play about 15 minutes with no explanation if possible; otherwise conduct a self-play with a written no-Inspector constraint.

Acceptance: a player can start, make at least one meaningful staffing/build/transport decision, and watch consequences.

Record where they got lost, what they thought the game was, what they watched, and what they ignored.
```


---

## Day 28 Prompt — Fix the top playtest failure + ratify only earned decisions

```text
Read Day 27's playtest note and fix the single highest-value problem first.

Then perform a planning/documentation reconciliation:
- promote verified implementation facts;
- mark explicit owner decisions;
- mark surviving provisional choices as hypotheses with triggers;
- return unresolved design questions to the backlog;
- remove failed hypotheses from active plans;
- write only the next three days at working depth.

Do not use the day to fill blanks just because the 28-day cycle ends.

Acceptance: the top playtest obstacle is materially improved, `git diff` shows planning docs accurately reflect reality, and the next three-day window is ready.

Produce `WHAT_WE_NOW_KNOW.md` with: visible game identity, stable seams, Fable decisions that survived, decisions that failed, current open questions, and the next three experiments.
```
