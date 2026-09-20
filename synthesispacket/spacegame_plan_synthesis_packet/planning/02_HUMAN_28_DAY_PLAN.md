# Human-Facing 28-Day Discovery Plan

## How to read this plan

This is a **28-day orientation map**, not a frozen 28-day implementation contract.

- **Days 1–3 are committed at working depth.**
- **Days 4–7 are the near horizon** and may be reordered by what Days 1–3 teach.
- **Days 8–28 are provisional daily shapes.** They say what question we expect to attack, not what API or exact design must exist.
- If today's observable fails, tomorrow starts by fixing it. The calendar slides; we do not stack new scope on a broken day.
- Every day ends with:
  - **Press Play and see…**
  - **What we expect to learn**
  - **Decisions this feeds**
  - **What we explicitly refuse to decide today**

The goal is still a playable colony builder. The difference is that the plan earns detail by watching the game instead of pre-authoring a month of assumptions.

---

# Days 1–3 — COMMITTED: make the colony visibly alive

## Day 1 — Replace capsules with people and give them real space to stand in

### In plain English

Before building another system, make one colonist look like a person and make one facility feel like a place rather than a cube.

Use the human character assets already available in the project. Keep the simulation object and its existing state intact; replace or attach the **visual body**, not the identity of the colonist.

Turn the Command Post and Farm blockouts into simple navigable surfaces. They can still be ugly. The important thing is that a human avatar can stand and move inside them.

### Work

- Capture a short baseline of the current sim before changing visuals.
- Put a human avatar/Animator under the colonist's presentation side while leaving `ColonistAgent`, employment, fatigue, location, and duty state authoritative where they are.
- Create the smallest useful NavMesh-ready surfaces for the Command Post and Farm.
- Prove a presentation-side avatar can move to a point inside a facility without changing the colonist's logical `currentLocation`.
- If geometry is moved under a mesh child, explicitly verify the simulation-root collider still matches the visible object; do not inherit a hidden 1×1×1 selection collider.
- Add only the transform/anchor convention needed **today**. Do not pre-author beds, corridor nodes, holding slots, EVA points, etc.

### Press Play and see

A real human avatar exists where a capsule used to be. The Farm and Command Post are walkable blockouts. You can move the avatar around the Farm locally while the simulation still considers the colonist to be "at the Farm."

### What we expect to learn

- Does the chosen avatar rig/Animator behave cleanly in this project?
- Is a presentation-child NavMeshAgent viable without fighting the simulation root?
- What scale/collider/doorway problems appear immediately?
- Which "socket" or interaction concepts are actually needed once a person exists in the space?

### Decisions this feeds

Animation ownership, local-vs-logical movement, facility interaction points, later cutaway/interior presentation.

### Not today

No universal animation controller, no corridor routing, no work-cycle schema for every facility, no final art, no UI.

---

## Day 2 — Port the interactable Farm and watch somebody actually work

### In plain English

Now connect the parallel interactable-facility prototype. Do **not** recreate it from this document if the prototype source is available; inspect and port the real thing.

The Farm should be able to tell an active worker something like:

> sit at this console → perform crop task A → perform crop task B → repeat

The exact 40-second example values are prototype timing, not game balance.

### Work

- Locate and read the parallel interactable-facility prototype before designing a replacement.
- Port the minimum reusable pieces required to run one Farm work cycle.
- Give the Farm explicit local interaction targets needed by that cycle.
- Trigger the visible cycle from the existing truth that the colonist is an **active worker at that facility**.
- Keep the current production system authoritative. The Farm's recipe and `FacilityPerformanceComponent` continue deciding actual output.
- The interaction system may direct the avatar's local navigation, pose, and animation; it must not independently decide that the worker is employed, qualified, productive, or generating resources.
- Log the actual seam discovered while porting: what the facility offers, what the worker/view consumes, and how a cycle is cancelled when duty ends.

### Press Play and see

A Farm worker arrives logically, walks between local Farm interaction points, visibly performs a repeating work sequence, and stops/relinquishes the activity when they are no longer an active worker.

### What we expect to learn

- Whether "facility offers a cycle; worker executes it" is the right final abstraction.
- Whether cycle steps should be data, components, or direct references to prototype objects.
- How animation, NavMesh, sit/stand transitions, and interruption actually behave.
- Whether multiple workers need unique stations immediately or can share a simple pool.

### Decisions this feeds

Facility presenter architecture, workstation/interactable authoring, animation-state ownership, later medical/movie/theatre/etc. interactions.

### Not today

No generic "every conceivable facility action" framework. No productivity coupling. No final timing. No attempt to solve beds, recreation, medical treatment, or every future activity.

---

## Day 3 — Make a shuttle park somewhere and make people visibly get on and off

### In plain English

The transport system already knows people need to move. Today makes that truth visible.

Do not build the final space-flight simulator. Use the movement system that exists and make it arrive at a real berth in a believable pose. Then synchronize bodies with boarding and disembarking.

### Work

- Add the minimum docking geometry consumed now: a berth/capture pose, an approach pose if the current mover needs it, and a passenger entry/exit point.
- Make one shuttle physically arrive and park at the Command Post and one destination.
- Preserve the existing responsible-pilot lease and transport-contract authority.
- Replace instant visual boarding with a visible sequence:
  - passenger is at the dock,
  - avatar walks to the shuttle entry,
  - board/containment commits at the correct boundary,
  - body hides or becomes ship-contained,
  - on arrival the reverse happens.
- Ensure cargo/passenger unload cannot visually occur before the ship is actually parked.
- Record awkward states instead of papering them over: ship at berth but passenger not ready, passenger ready but ship not parked, duty ending while boarding, etc.

### Press Play and see

A human walks to a shuttle, boards it, the shuttle leaves, physically parks at the other location, and the human visibly gets off and proceeds into the destination.

### What we expect to learn

- Which docking states are genuinely needed.
- Whether ports need reservation before we have more than one ship.
- Whether current kinematic movement is visually adequate.
- Where simulation arrival and presentation arrival need synchronization.
- Whether passenger boarding belongs inside the transport executor, ship voyage authority, or a separate handoff component.

### Decisions this feeds

Voyage authority, docking control, port queues, passenger handoff semantics, later flight-model work.

### Not today

No custom Newtonian 6DOF integrator. No universal queue policy. No authored holding-slot arithmetic. No final load/unload duration.

---

# Days 4–7 — NEAR HORIZON: stabilize the seams Days 1–3 exposed

## Day 4 — Separate the scene/presentation responsibilities we now actually need

### In plain English

After three days of touching the real visible slice, clean up the structure around what exists instead of guessing what future systems will need.

### Work

- Split scene ownership only as far as it now helps: managers/simulation, base/content, presentation/environment, and UI if/when needed.
- Make presentation assemblies depend on runtime, never the reverse.
- Consolidate the tiny anchor/socket convention based on Days 1–3's real consumers.
- Preserve working avatar, Farm cycle, and docking behavior through the split.
- Fix any collider/selection/navmesh reference breakage caused by prefab/mesh-root changes.

### Press Play and see

The same Day-3 loop still works from the cleaned-up scene structure.

### Learn

Which scene boundaries actually reduce collisions between future agents, and which cross-scene references should become registries/lookup.

### Not today

No future socket list. No scenario bootstrap.

---

## Day 5 — Split the staffing hotspot, part 1

### In plain English

Now pay down the 1,000+ line `StaffingManager` before walking/automation adds more behavior to it.

### Work

- Characterize current staffing behavior after the first four days.
- Extract reporting/formatting and explicit employment responsibility behind the same public facade.
- Preserve every existing assignment result and validation rule.
- Treat line count as a smell, not an acceptance number.

### Press Play and see

The visible worker and shuttle loop behaves exactly as before; manual assignment results are unchanged.

### Learn

Whether the intended boundaries in the old P0-A refactor still match the code after the visual integration.

### Not today

No allocator. No new staffing events "for later."

---

## Day 6 — Split the staffing hotspot, part 2

### Work

Extract commute batching, pilot reconciliation, and colonist reconciliation as the current code naturally allows. Preserve the existing `StaffingManager` facade and duty-state ownership.

### Press Play and see

A full shift change, pilot handoff, Farm work cycle, and shuttle trip behave the same as the pre-split baseline.

### Learn

Where commute decisions really live now, and which exact method should later ask "walk or shuttle?"

### Not today

No new route algorithm until the split is proven.

---

## Day 7 — Add the smallest walk-vs-shuttle routing decision

### In plain English

Now implement the strategic rule the owner already chose: if there is a valid walk connection, people walk; otherwise they use the shuttle; if neither works, they are visibly blocked.

### Work

- Author one explicit walk connection between Command Post and Farm.
- Add the smallest resolver that can answer Walk / Ship / Unreachable.
- Do not solve the whole future construction-placement graph.
- Let the simulation own logical transit and arrival; let presentation/NavMesh own the body moving through space.
- Mark walking speed/traversal time as a tunable placeholder to be set by watching the actual human.

### Press Play and see

Farm staff walk. A worker going to the shuttle-served destination flies. Break the walk connection and the worker switches modes or becomes visibly blocked.

### Learn

Whether authored links plus NavMesh presentation are sufficient, and how much of the route needs to be simulation data versus local navigation.

### Not today

No Dijkstra requirement unless multiple paths exist. No corridor damage/pressurization. No construction placement rules.

---

# Days 8–14 — PROVISIONAL: management and movement become understandable

## Day 8 — Make the walking commute look correct

Use the NavMesh/avatar work from Days 1–2 to make an inter-facility walk visually believable. Solve doorway transitions, corridor entry/exit, animation, and synchronization with logical arrival.

**Press Play and see:** worker leaves home, walks to the Farm, enters, starts the Farm work cycle, later leaves and goes home.

**Learn:** whether sim ETA should drive the body, the body should gate arrival, or a hybrid handoff is needed.

**Do not decide:** universal pathfinding for every future base shape.

---

## Day 9 — Give ships one voyage owner without changing flight feel

Consolidate the duplicated "go there / arrive / dock" authority behind one voyage component or service, but keep the currently working movement model.

**Press Play and see:** transport, extraction, and crew-return callers request voyages through the same owner and the Day-3 dock/boarding sequence still works.

**Learn:** what phases are actually necessary after implementing real boarding/docking.

**Do not decide:** 6DOF versus PhysX versus kinematic final movement.

---

## Day 10 — Create deliberate dock contention

Add a second ship or otherwise force two ships to want the same berth. Watch the failure before designing the queue.

Implement only the minimum reservation/holding behavior needed to prevent overlap/deadlock.

**Press Play and see:** two ships contend for one berth without occupying the same physical space or losing their obligations.

**Learn:** whether queue priority matters yet, how holding should look, and whether one authored holding point is enough.

**Do not decide:** final queue ordering policy, modulo holding slots, port-module construction.

---

## Day 11 — Build the first real management panel

Select the Farm and answer the immediate questions you have after ten days of watching it:

- what is it producing?
- what is blocking it?
- who is assigned?
- who is active?
- what is the visible worker currently doing?

Use the fastest UI technology that works in the actual project. Do not declare a permanent project-wide UI stack from this one panel.

**Press Play and see:** click the Farm and understand its current state without opening the Inspector.

**Learn:** what data deserves to be visible all the time versus only while selected.

**Do not decide:** final HUD, full panel family, global style system.

---

## Day 12 — Manual HR: expose what the simulation already knows

Make one staffing interaction usable: choose a workplace/role/shift, see candidates and exact rejection reasons, assign/unassign.

Do not pre-design the final HR layout.

**Press Play and see:** reassign a Farm worker and watch the physical commute/work cycle change.

**Learn:** whether the player thinks job-first, person-first, or target-count-first.

**Do not decide:** allocator algorithm, filters, badges, full-screen/modal behavior.

---

## Day 13 — Add raw observability, not a dashboard

Expose the event/data feeds needed to answer later questions:

- in-memory history transition feed,
- raw resource deltas or event samples,
- blocked intervals/transitions.

Store facts; derive summaries only when a screen actually needs them.

**Press Play and see:** cause one shortage/block/ship hold and inspect a clean chronological record without parsing JSONL.

**Learn:** which events are noisy, missing, or ambiguous.

**Do not decide:** 24-hour buckets, `HoursUntilEmpty`, report tables, alert thresholds.

---

## Day 14 — Build one "why isn't this working?" explanation

Take the single most frustrating diagnostic question discovered in Days 1–13 and answer it in the UI.

This may be a facility blocker sentence, a transport wait reason, or a staffing rejection explanation. The observed pain chooses the work.

**Press Play and see:** deliberately create that failure and get a useful explanation in-game.

**Learn:** whether the reporting layer should be event-first, state-first, or both.

**Do not decide:** colony report structure.

---

# Days 15–21 — PROVISIONAL: build, staff, shortage, housing

## Day 15 — Drag one buildable object

Create the smallest construction-placement experiment:

- one temporary test building definition,
- a movable/rotatable ghost,
- overlap validation,
- confirm creates a site marker.

Use free placement first unless Days 1–14 have already produced evidence for snapping.

**Press Play and see:** drag, rotate, reject overlap, place a site.

**Learn:** how placement feels with the actual camera and modules.

**Do not decide:** 15° snap, SitePlane IDs, corridor max length, full build menu, tech unlock fields.

---

## Day 16 — Make that site receive material and visibly get built

Reuse the systems that already exist:

- local site inventory,
- a temporary explicitly non-canonical construction material,
- stock demand,
- Builder staffing,
- facility performance,
- the interactable-facility/work-cycle presentation for builders.

**Press Play and see:** material arrives, a builder physically works, and the test building completes.

**Learn:** whether construction feels like logistics+labor or needs another concept.

**Do not decide:** canonical construction chain/material, five building types, refund formula.

---

## Day 17 — Build one corridor and watch strategy change

Give the player the cheapest possible corridor interaction needed to connect two existing endpoints. Completion creates the walk link used by Day 7.

**Press Play and see:** build the corridor to a previously shuttle-served workplace; next commute walks. Remove/disable it; commute changes again.

**Learn:** whether corridor placement wants node snapping, free endpoints, modules, or another interaction.

**Do not decide:** full corridor geometry rules until this has been used.

---

## Day 18 — Staffing targets are information first

Add target headcount per role/shift and priority/pin data only to the extent already chosen by the owner. Show assigned vs target in HR.

No allocator yet.

**Press Play and see:** set Farm target to 2, see 1/2, manually assign a second worker, watch the gap close.

**Learn:** whether manually filling targets feels satisfying or tedious.

**Do not decide:** preemption, release order, hysteresis, allocator cadence.

---

## Day 19 — Trigger-gated allocator experiment

If Day 18 made you wish the game would fill obvious gaps automatically, add the smallest allocator:

- fill an under-target role through the existing assignment API,
- do not move an already-employed worker unless the owner explicitly chooses that behavior,
- no invented hysteresis until thrash appears.

If Day 18 did **not** create that desire, spend this day fixing the largest staffing-management pain discovered instead.

**Press Play and see:** the specific pain from Day 18 is reduced without creating surprising job churn.

**Learn:** whether automation is helping or taking agency away.

---

## Day 20 — Show shortage before inventing personal hunger

Use the existing aggregate consumption shortage truth. Make it visible in the world/UI/history.

Disable the Farm or otherwise force food shortage.

**Press Play and see:** the colony clearly communicates "we are short of Food" at the place the shortage occurs.

**Learn:** whether aggregate scarcity is enough to create the intended pressure.

**Do not decide:** per-colonist nutrition/hydration, ration allocation, work penalties, death clocks.

---

## Day 21 — Make housing visible before giving homelessness a penalty

Use existing home/capacity facts and the visible facility interaction system:

- show who has a home/bed,
- make sleeping/rest visibly use bed interaction points where available,
- keep runtime capacity authoritative,
- add manual home assignment only if needed to conduct the experiment.

Create a deliberate housing shortage as **test setup**, not as the canonical starting scenario.

**Press Play and see:** a colonist lacks a bed/home and the game names that fact; adding housing resolves it visibly.

**Learn:** whether homelessness should block sleep, reduce recovery, cause morale, or simply be a warning.

**Do not decide:** `0.5` restfulness or auto-homing.

---

# Days 22–28 — PROVISIONAL: lifecycle, scenario, diagnosis, presentation, playtest

## Day 22 — Kill one colonist manually and discover cleanup truth

Build one explicit debug/development kill command whose job is to ask each subsystem to release the colonist cleanly and report failures.

Use this to discover teardown order; do not encode a starvation rule.

**Press Play and see:** manually kill a colonist during a difficult state—working, walking, passenger, or pilot—and no active obligation keeps a dead reference.

**Learn:** which systems self-clean and which require explicit release.

**Do not decide:** what automatically causes death.

---

## Day 23 — Make New Game recreate the slice that actually exists

Do not pre-author a future-shaped scenario schema.

Build the minimum representation/bootstrap required to recreate the current working slice, derived from the objects and content that now actually exist. Prove it by round-trip.

**Press Play and see:** create/capture/configure the current colony, start New Game, and get the same functional slice back.

**Learn:** which state is authored scenario data versus runtime state.

**Do not decide:** future site/ship/deposit/colonist fields that current content does not need.

---

## Day 24 — Run the current economy and tune only what prevents observation

Run a long-enough accelerated session to see:

- Farm production,
- Food/Water consumption,
- shuttle congestion,
- worker commute/work timing,
- construction throughput if present.

Change only values that make the slice impossible to observe or obviously degenerate. Record every tuning value as a dial, not a law.

**Press Play and see:** the loop runs long enough to form opinions rather than immediately deadlock.

**Learn:** starting stock/headcount/bed/ship pressure from evidence rather than arithmetic in a planning document.

---

## Day 25 — Build the report/alerts you actually wished existed on Day 24

Review Day 24 notes. Implement the one or two missing explanations/alerts that would have changed your decisions.

Thresholds should be tuned from observed distributions, not invented in prose.

**Press Play and see:** reproduce the failure and receive the information you wished you had during the run.

**Learn:** whether a larger colony report is necessary at all.

---

## Day 26 — Presentation/camera/interior pass based on the real loop

Now that people walk, work, sleep, board and dock, decide how the player should *see* tight interiors.

Try the smallest useful presentation treatment: cutaway, hidden roof, selected-module reveal, windows, or another solution based on the actual geometry.

Also make a focused environment/lighting pass around the active slice rather than generating a whole aesthetic pipeline in advance.

**Press Play and see:** you can follow one colonist from home → commute → work → shuttle → destination without losing them in the presentation.

**Learn:** the actual art/camera constraints of the cramped-space game.

**Do not decide:** final environment generator, LOD budgets, campaign-wide art pipeline.

---

## Day 27 — Put a thin session frame around what exists and hand it to another person

Add only the session controls needed for an unaided playtest: start/restart, pause/speed if useful, and quit/menu as necessary.

Do not add a "game over" condition unless the project has actually decided what failure means.

Have another person play for about 15 minutes with no explanation.

**Press Play and see:** a new player can enter the slice, manipulate at least one meaningful thing, and watch consequences.

**Learn:** what they think the game is, where they get lost, and which system feels like the game rather than plumbing.

---

## Day 28 — Fix the highest-value failure and ratify only earned decisions

Take the top problem from Day 27 and fix it.

Then perform a documentation pass:

- promote facts that are now proven,
- record owner decisions explicitly,
- move still-open questions back to the decision backlog,
- delete or relabel hypotheses that failed,
- write only the **next three days** at working depth.

**Press Play and see:** the playtest's biggest obstacle is materially better.

### End-of-cycle deliverable

A one-page "What we now know" note answering:

1. What visibly makes this feel like the intended game?
2. Which architecture seams proved stable?
3. Which old Fable decisions survived actual play?
4. Which ones died?
5. What are the next three experiments?

---

# What this 28-day plan deliberately does not lock

The roadmap may mention these because they are foreseeable, but none is a current contract:

- custom Newtonian 6DOF flight,
- final docking queue policy,
- exact load/unload durations,
- project-wide UI technology,
- full HR/report/HUD layouts,
- personal nutrition/hydration,
- starvation/death thresholds,
- homelessness penalty,
- canonical construction material chain,
- full `BuildingDefinition` field list,
- fixed SitePlane/multi-plane placement model,
- full starting scenario numbers,
- ice depletion on Day 5–8,
- final environment generator,
- Phase-1 exploration schema.

Those become designs when an experiment produces the information needed to design them.
