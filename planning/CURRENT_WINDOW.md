# Current Three-Day Window — Human Work + Physical Shuttle

> **Execution authority.** The 28-day roadmap is orientation. This file is the next
> commitment. If a day's observable is broken, the next day repairs/learns before new
> scope starts.

## Day 1 — Human avatars and navigable facility blockouts

### Goal

Replace one capsule visual with an existing human avatar and make the Command Post and
Farm blockouts navigable without moving simulation authority into presentation.

### Allowed surface

- Current scene and the presentation-side avatar/prefab content needed to show it.
- Existing imported human content under `Assets/PolygonSciFiWorlds`.
- NavMesh surfaces/agent configuration and the minimum local authoring anchors.
- Small presentation/runtime seam changes only when the current code proves they are
  required. Do not touch unrelated settings, imported materials, or the camera's
  manually authored placement.

### Work

- Capture a baseline before changing the visual body.
- Keep `ColonistAgent`, employment, fatigue, duty, logical location, and facility
  performance authoritative where they are.
- Put the Animator/NavMesh behavior on a presentation child or equivalent visual side.
- Make the Command Post and Farm simple walkable blockouts.
- If mesh geometry moves below a root, verify the root collider still covers the object;
  do not inherit a hidden 1×1×1 selection collider.

### Press Play and see

A human avatar moves between two points inside the Farm on navigable geometry while the
simulation still reports the colonist at the Farm and existing simulation behavior is
unchanged.

### What we expect to learn

Avatar scale/rig behavior, local NavMesh viability, doorway/collider problems, and the
smallest anchor convention the real scene consumes.

### Decisions this feeds

Avatar/presentation ownership, local-versus-logical movement, facility interaction
points, and later interior visibility.

### Explicitly not deciding

No universal animation controller, corridor routing, work-cycle schema, final art,
camera/cutaway technique, UI, or future socket list.

### End-of-day result

- Confirmed:
- Falsified:
- New questions:
- Backlog triggers fired:
- Carry/fix into Day 2:

## Day 2 — Port the real interactable Farm work cycle

### Goal

Use the existing local source at `Packages/com.asteroidcolony.interactions` to make an
active Farm worker navigate between authored local targets and visibly repeat a work
cycle.

### Allowed surface

- The interaction package runtime/editor code only where the current integration needs a
  compatible seam.
- Farm/Command Post facility content, activity bindings, anchors, targets, animation
  controller wiring, and presentation-side avatar components.
- Existing runtime staffing/recipe/inventory code only for the narrow read/command seam
  that proves active-worker truth.

### Work

- Inspect the package source before designing changes; do not recreate its activity or
  sequence model from prose.
- Port the minimum facility content needed for one Farm worker cycle.
- Start it from the existing fact that the colonist is an active worker at the Farm.
- Ensure duty ending, cancellation, or loss of active-worker status releases the local
  reservation and stops the visual cycle cleanly.
- Record the actual seam: facility data supplied, avatar/view data consumed, cycle-state
  owner, and cancellation boundary.

### Press Play and see

An active Farm worker arrives, performs the imported local cycle, repeats it, and stops
cleanly when the worker's duty ends. Production still comes from the existing
recipe/inventory/facility-performance path.

### What we expect to learn

Whether facility-offers-cycle/worker-executes-cycle is the right shape, what data must
be facility-owned, how interruption behaves, and whether one worker needs a unique
station immediately.

### Decisions this feeds

Facility presenter architecture, workstation authoring, animation ownership, and future
facility-specific interactions.

### Explicitly not deciding

No universal action graph, production coupling, final timing, bed/medical/recreation
activities, or generic facility schema.

### End-of-day result

- Confirmed:
- Falsified:
- New questions:
- Backlog triggers fired:
- Carry/fix into Day 3:

## Day 3 — Physical shuttle docking and visible boarding

### Goal

Make one existing shuttle physically arrive at a real berth and synchronize visible
boarding/disembarking with transport truth.

### Allowed surface

- Existing ship movement, transport-contract, passenger-carrier, pilot-lease, and
  presentation code.
- Minimum berth/capture, approach, and passenger entry/exit transforms consumed by the
  observable.
- Ship/facility scene content required to author those poses.

### Work

- Read current movement and carrier ownership before editing.
- Preserve transport contracts, passenger containment, logical arrival, and responsible
  pilot lease as the authorities.
- Make the body walk to the entry, commit boarding at the real boundary, hide/contain it,
  then reverse the process after the ship is actually parked.
- Record awkward states instead of adding speculative fallbacks.

### Press Play and see

A human walks to the shuttle, boards, the shuttle departs and parks at the destination,
and the human visibly disembarks and proceeds into the facility. Cargo/passenger
completion remains correct.

### What we expect to learn

Which docking/boarding states are real, where simulation and presentation need a
handoff, whether the existing movement feel is adequate, and which contention case is
worth forcing next.

### Decisions this feeds

Voyage authority, docking state, berth reservation, passenger handoff, and later flight
model work.

### Explicitly not deciding

No custom Newtonian 6DOF, PhysX choice, final queue priority, modulo holding slots,
universal load/unload timing, or full scenario schema.

### End-of-day result

- Confirmed:
- Falsified:
- New questions:
- Backlog triggers fired:
- Carry/fix into Day 4:
