# S03 — `ShipVoyageComponent`

Depends on S01 and S02. Read the historical §3 proposal in `01_LOCKED_DESIGN.md`; the
phase machine is provisional until the current docking/boarding work demonstrates it.

## Must create
- `Assets/Scripts/ColonyPrototype/Vehicles/ShipVoyageComponent.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/VoyageTypes.cs` (enums + result types)
- `Assets/Tests/EditMode/ShipVoyageTests.cs`

## May modify
- `Assets/Scripts/ColonyPrototype/Vehicles/ShipComponent.cs` — add `[RequireComponent(typeof(ShipVoyageComponent))]`,
  make `BeginUndocking/BeginDocking/MarkDeparted` `internal`, derive `MovementPhase`
  from the voyage when present. **Do not** remove them yet (S04 callers still compile).

## Steps
1. Implement the phase machine exactly as written, one `switch` per tick, one
   `SetPhase(next, reason)` helper that records `ship.phase` history.
2. Substep loop per §3 "Integration"; guidance per substep.
3. Write the ship root transform once per tick after integration.
4. Blocked retains the movement lease; `TryAbortToNearestBerth` is the only way out
   apart from the blocker clearing.

## Tests (use a fixture profile with high thrust so voyages finish in few ticks)
1. Docked → `RequestVoyageToBerth` → phases pass through Undocking, Cruise,
   RequestingBerth, Approach, FinalDocking, Docked; `ship.CurrentDock == station`;
   movement lease released at the end; `TryCompleteArrival` was called with the owner.
2. Destination has one port already Occupied → phase reaches `Holding`,
   `QueuePosition == 0`; releasing the other ship's berth → Approach → Docked.
3. Destination has no `DockingControlComponent` → `Blocked`, reason names the station;
   adding a control at runtime → resumes to Approach/Docked.
4. Request while not Docked → `AlreadyUnderway`; lease owned by another owner →
   `MovementOwnedByOther`; null profile → `MissingProfile`; nothing mutated.
5. Loiter voyage: reaches `Loitering` at the point; then a Berth voyage from Loitering
   works.
6. Undocking releases the origin berth exactly once and only after push-off completes.
7. Disable the component mid-Cruise → no movement; re-enable → continues from the
   serialized flight state (no teleport).

## Forbidden
- Touching the three caller components (S04).
- Any Presentation code.

## Acceptance
- [ ] `ShipVoyageComponent.cs` ≤ 400 lines; if larger, extract the per-phase handlers
      into `VoyagePhaseHandlers.cs` (static, internal).
- [ ] Observable: in a scratch scene with two stations authored per §1, issuing a
      voyage from the Inspector context menu shows the ship push back, turn, burn,
      flip, brake, align and capture; `ReadinessHistory` lists each `ship.phase` change.
