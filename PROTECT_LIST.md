# Protect List — Do Not Lose These While De-Specifying

## Code-grounded invariants

1. **Inventory is resource quantity authority.** Other systems request/mutate through its APIs; UI/views do not carry a second stock truth.
2. **Resource converters do not count workers.** They consume `FacilityPerformanceComponent` state/multipliers.
3. **Facility influences use performance-provider channels.** Staffing is a provider, not a recipe concern.
4. **Employment is explicit.** `EmploymentAssignment` is the fact; assignment changes go through validated staffing commands.
5. **Logical location means last arrived location.** Transit is separate from arrival.
6. **Responsible pilot is a temporary ship-operating lease**, separate from employment.
7. **Publish then commit for freight.** Demand/supply can exist cheaply; reservation happens when real work is materialized/assigned.
8. **Extraction is not ordinary freight arbitration.**
9. **Simulation state advances through simulation ticks**, not presentation frame time.
10. **Views/presentation do not become a second simulation authority.**
11. **Registries are preferred over hot-path scene scans.**

## Owner-stated product intent

1. **People must be physically visible doing the colony's work.** Walking, working, boarding, sitting, sleeping, and similar activity are core to the game's legibility.
2. **Cramped, small, spaceborne facilities are the intended feel.** Exact cutaway/camera technique remains open, but "big flat rooms" is not the target.
3. **Corridors and shuttles are both first-class movement modes.** Layout should visibly change transport pressure.
4. **Staffing is facility-level, not embedded in recipes.**
5. **Hybrid staffing control is a human choice:** targets/priority/manual pinning are valid concepts; the autonomous allocator behavior is not yet decided.
6. **Daily work should end in a visible Play Mode result.**
7. **The next day's work should be informed by what today taught.**

## New near-term integration boundary

### Interactable facilities

The parallel interactable-facility prototype is valuable and should be integrated early.

For the first integration:

- Runtime says whether a colonist is an active worker at a facility.
- The facility/presentation layer may offer local tasks/interactions.
- The avatar executes local NavMesh movement, poses, and animation.
- Runtime production remains governed by recipe + inventory + facility performance.
- Completing "pick potatoes" does not independently add Food unless a later owner decision deliberately makes task execution gameplay-authoritative.

This prevents a visible-work system from becoming an accidental second economy.

### NavMesh / avatar movement

For the first days:

- `ColonistAgent.currentLocation` stays logical truth.
- Local movement inside an already-arrived facility is presentation.
- Inter-facility walking must explicitly define a handoff between logical transit and NavMesh presentation when that feature arrives.
- Do not simply attach a NavMeshAgent to the simulation root and let frame-time movement overwrite simulation truth.

### Docking / boarding

Protect existing transport authority:

- contract = obligation;
- passenger carrier = who is actually aboard;
- responsible pilot = operating lease;
- ship/voyage state = where the ship is / what movement owns it.

Visible boarding/disembarking must project or gate these truths, not replace them.

## Structural moves worth keeping

- Split `StaffingManager` by responsibility behind a stable facade.
- Consolidate duplicate ship movement ownership into one voyage authority.
- Additive scene/assembly separation where it prevents agent conflicts and runtime/presentation coupling.
- A minimal socket/anchor convention, grown only when an immediate consumer appears.
- Raw in-memory event/history feeds for UI observability.
- Narrow validated commands returning explicit results.
- "Neither transport mode works" should be a visible blocker, not a silent teleport/fallback.
- Per-waypoint/explicit arrival semantics are preferable to lying about where a colonist is.

## Process moves worth keeping

- Commit/review in small tickets.
- Preserve reasoning trails.
- Treat constants in content assets as tunable dials unless explicitly ratified.
- Keep future work visible as questions with triggers rather than hidden assumptions.
- When the observable fails, fix it before starting new scope.
