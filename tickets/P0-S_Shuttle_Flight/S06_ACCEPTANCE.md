# S06 — Packet Acceptance

## Structural
- [ ] Runtime asmdef has no reference to the Presentation asmdef.
- [ ] `grep -rn "Rigidbody\|FixedUpdate\|Time.deltaTime" Assets/Scripts/ColonyPrototype/` → no hits in Runtime.
- [ ] `grep -rn "transform.position =\|SetPositionAndRotation" Assets/Scripts/ColonyPrototype/` → only `ShipVoyageComponent.cs` (ships) and any pre-existing non-ship writes listed in S00.
- [ ] Exactly one writer of `VoyagePhase`; `ShipComponent.MovementPhase` is derived.
- [ ] No file > 400 lines in `Vehicles/`, `World/Docking/`, `Presentation/Ships/`.
- [ ] Every ship-visited anchor in the scene has a `DockingControlComponent` with ≥1 port, except the deposit.

## Behavioral (fresh Play Mode, 72 game-hours, 1× then 10×)
- [ ] All freight/passenger contracts that completed before the packet still complete.
- [ ] Mining loop: loiter → extract → berth → unload → repeat.
- [ ] Port contention: with two shuttles and one Farm port, `Holding` occurs, queue
      order respects priority, and the holder docks after release with no deadlock.
- [ ] Closing a port (disable) while a ship is Holding keeps it Holding; reopening
      grants it.
- [ ] Removing a station's `DockingControlComponent` while a ship is en route yields
      `Blocked` naming the station; restoring it resumes.
- [ ] `TryAbortToNearestBerth` from Holding returns the ship and releases the lease.
- [ ] Timed loading/unloading visible via `TransferProgress01`; inventory totals
      conserved.
- [ ] Pilot fatigue accrues for the whole voyage including Holding (existing rule).

## Look
- [ ] Reviewer watches three voyages at 1× and 4× and signs off on §5's slickness
      checklist in writing.

## Docs
- [ ] `ARCHITECTURE.md` ship composition lists `ShipVoyageComponent`, ports, dock control,
      and the Presentation layer; tick table unchanged at 400.
- [ ] `CONTENT_AUTHORING.md` explains authoring ports, holding slots, and a flight profile.

Write `ACCEPTANCE_REPORT.md` with evidence per checkbox.
