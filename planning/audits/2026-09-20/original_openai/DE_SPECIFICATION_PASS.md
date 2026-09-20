# De-Specification Pass — Five Worst Offenders Rewritten as Discovery Tickets

These are replacements for over-specified portions of the tentative plan. They intentionally do **not** contain the final answer.

---

# DS-01 — Ship Movement / Voyage Discovery Spike

**Replaces:** P0-S S02/S03 implementation lock (“custom 6DOF Newtonian integrator, no PhysX,” exact profile fields, guidance algorithm, phase machine).

## Question
What movement behavior and simulation ownership are actually required for ships to be watchable, deterministic enough for this game, and understandable at the intended play speeds?

## Work
1. Characterize the current ship movement path and every writer/owner involved.
2. Centralize duplicated “go to destination and report arrival” ownership only as far as the current behavior requires; preserve current movement semantics for the baseline.
3. Build a bounded visual/movement spike that allows the human to compare the current behavior against **one** alternative implementation without migrating every caller.
4. Instrument only what is needed to compare: travel time, pause/speed behavior, arrival correctness, and visible motion.

## Press Play and see…
The same representative trip can be watched under the baseline and the spike alternative without changing logistics, staffing or extraction rules.

## Expected learning
- What looks wrong about current movement, specifically.
- Whether presentation smoothing solves most of the problem.
- Whether full orientation/acceleration state is actually needed in simulation.
- Which states need to be authoritative versus presentation-only.

## Do not decide in this ticket
- Physics technology.
- 6DOF vs. simpler kinematic model.
- Mass/thrust/RCS/inertia values.
- Burn-flip-burn guidance.
- Docking queue policy.
- Final voyage enum.
- Transfer durations.

## Last responsible moment
Before migrating all movement callers to a new production movement model.

## Decision points fed
DB-010 through DB-016.

---

# DS-02 — Building Placement Interaction Spike

**Replaces:** Day 15 / P0-C placement design (`SitePlane`, node snapping, 15° rotation, straight corridors, full `PlacementResult`).

## Question
What placement interaction makes the actual module shapes easy to position and connect in the game camera?

## Preconditions
- At least 2–3 representative placeholder module shapes exist at believable scale.
- Camera/selection are usable enough to manipulate them.

## Work
1. Make a **throwaway placement sandbox**, not production construction architecture.
2. Allow the human to move/rotate one module and attempt one connection between two modules.
3. Prototype only the interaction variants needed for comparison. Values used for snapping are spike-only.
4. Record friction: precision, readability, accidental overlap, difficulty connecting, camera problems.

## Press Play and see…
The human can place the same small arrangement more than once and can say which interaction feels controllable.

## Expected learning
- Whether free placement, snapping, nodes, grid-like behavior, or another interaction is actually desirable.
- Whether a plane is meaningful to the player or merely an implementation detail.
- Whether corridors should be straight, segmented, flexible, or deferred.

## Do not decide in this ticket
- `SitePlane` as production architecture.
- 15° rotation.
- Node schemas.
- Corridor max length/straightness.
- Multiple-site future-proofing.
- Production `PlacementResult` taxonomy.

## Last responsible moment
Before the first production `TryPlace` API or `BuildingDefinition` placement fields are committed.

## Decision points fed
DB-036 through DB-039.

---

# DS-03 — Survival Pressure Discovery

**Replaces:** P0-D Needs/Death/Housing numbers and Day 20–21 behavior.

## Question
When the colony cannot supply required Food/Water, what information and consequence make the shortage understandable and worth responding to?

## Work
1. Expose existing consumption success/failure per colonist or population in the simplest observable form available.
2. Create a controlled shortage in a test scene/session.
3. Record duration, visibility, player reaction, and recovery behavior **without death timers or permanent penalties**.
4. If the shortage is not legible, improve observability before inventing consequences.

## Press Play and see…
The player can create a shortage, identify who/what is not being supplied, and restore supply. Nothing dies merely because the spike needs an ending.

## Expected learning
- Whether needs should be individual values, discrete states, or something else.
- How much warning is needed.
- Whether shortage should affect work, fatigue, health, morale, death, or a subset.
- Whether housing belongs in the same loop or is a separate question.

## Do not decide in this ticket
- Nutrition/hydration 0..1 schema.
- 0.4 work/recovery multiplier.
- 0.3 alert threshold.
- 24h/72h death timers.
- Homeless restfulness 0.5.
- Auto-home behavior.

## Last responsible moment
Before persistent needs/consequence state is added to colonists.

## Decision points fed
DB-049 through DB-055.

---

# DS-04 — Scenario Schema Discovery

**Replaces:** Day 22 / P0-D full `ScenarioDefinition` field list.

## Question
What authored facts are actually required to create the starting colony through the stable production paths that exist by then?

## Preconditions
Do not start this ticket until the real placement/construction/transit/docking/staffing paths needed for the chosen start are stable enough to assemble the start manually.

## Work
1. Assemble the intended **test start** manually using current production authoring paths.
2. Do it a second time from an empty base/scene and record every fact that had to be authored or chosen.
3. Separate those facts into:
   - stable content references;
   - runtime state that should not be authored;
   - editor/scene convenience that does not belong in a scenario format.
4. Only after the list is observed, propose the smallest data container capable of expressing it.

## Press Play and see…
Two manually assembled starts behave the same without relying on a speculative scenario asset.

## Expected learning
- The actual minimum scenario schema.
- Which IDs/references must survive spawning.
- Which construction paths are suitable for bootstrap and which are player-only workflows.

## Do not decide in this ticket
- `sitePlanes[]`.
- full module/corridor/ship/deposit/colonist nested structures.
- clock defaults in scenario data.
- exact starting population/facility composition.

## Last responsible moment
Immediately before external playtest needs reliable New Game/reset behavior.

## Decision points fed
DB-056 through DB-060.

---

# DS-05 — Reporting and Alert Discovery

**Replaces:** Day 23 / P0-B U07 report schema and fixed alert thresholds.

## Question
What information does the human fail to notice or reconstruct while actually playing the current colony?

## Preconditions
- A usable minimal management UI exists for at least one facility/ship/colonist task.
- Run at least two real play sessions without a colony-report screen.

## Work
1. During play, write down every question the human asks that the world/current panels cannot answer quickly.
2. Mark each question as:
   - current state;
   - trend/history;
   - causal explanation;
   - warning/alert.
3. Pick **one** highest-value unanswered question.
4. Add the smallest read model or view that answers only that question.
5. If it is an alert, derive the threshold by observing when the warning would have been useful; do not start from a round number.

## Press Play and see…
A previously demonstrated information failure is now answered in the game without opening the Unity Inspector.

## Expected learning
- Whether a colony-wide report is necessary at all.
- Which aggregates deserve stable ownership.
- What warning horizon is useful.
- Which explanations players can infer from the world instead of a table.

## Do not decide in this ticket
- Population/resource/blocked/duty/transport section list.
- Top(20).
- 24h report window.
- 4Hz evaluation.
- 12h / 2h / 1h / 8h alert thresholds.
- exact prose templates.

## Last responsible moment
One proven information failure at a time.

## Decision points fed
DB-020 through DB-030.
