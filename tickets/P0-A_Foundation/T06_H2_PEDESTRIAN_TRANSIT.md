# T06 (H2) — `PedestrianTransitComponent` and `ColonistActivity.Walking`

Depends on T05. Read the historical Part 2 proposal in `01_LOCKED_DESIGN.md`; its tick
semantics are not a current contract until the visible commute experiment needs them.

## Goal
A colonist can execute a walk plan with truthful per-waypoint arrival. Nothing
requests walks yet (T07).

## Must create
- `Assets/Scripts/ColonyPrototype/World/Transit/PedestrianTransitComponent.cs`
- `Assets/Tests/EditMode/PedestrianTransitTests.cs`

## May modify
- `Assets/Scripts/ColonyPrototype/People/ColonistAgent.cs` — add `Walking` to the enum
  (append), add the `Pedestrian` accessor. Nothing else.
- `Assets/Person.prefab` — add the component.
- `Assets/Scripts/ColonyPrototype/Vehicles/PassengerCarrierComponent.cs` — boarding
  refuses a walking colonist (one guard clause).

## Tests to write
1. `BeginWalk([A,B])` from `currentLocation == A`: `IsWalking`, `currentLocation` still
   A, `colonist.IsInTransit` true, `TransitDestination == B`.
2. Ticking `traversalHours` total: `currentLocation == B`, `State == Idle`,
   `IsInTransit` false.
3. Path `[A,B,C]`: after first leg `currentLocation == B` and `TransitDestination == C`.
4. Disabling the A–B link mid-leg → `State == Blocked`, `currentLocation == A`,
   `BlockReason` names the link.
5. `BeginWalk` while walking to a different destination returns false and mutates
   nothing.
6. `BeginWalk` where `path[0] != currentLocation` returns false.
7. `Cancel()` mid-leg → Idle, `currentLocation` unchanged, `IsInTransit` false
   (call `colonist` transit reset through the existing API — if `ColonistAgent` needs a
   `CancelTransit()` to do this cleanly, add exactly that one method and report it).
8. `PassengerCarrierComponent` boarding returns false for a walking colonist.

## Forbidden
- Moving the transform. No `Update`. No presentation.
- Caching `TransitLinkComponent` references across ticks.
- Any reference to `StaffingManager` or duty state.

## Acceptance
- [ ] Component ≤ 220 lines; registers at priority 400 in `OnEnable`, unregisters in
      `OnDisable`.
- [ ] `ReadinessHistory` records `transit.walk.begin/leg/blocked/cancel`.
- [ ] Observable: in Play Mode, on a colonist with a link authored (scratch scene),
      calling `BeginWalk` from a debug context shows `LegProgress01` climbing in the
      Inspector and `currentLocation` flipping exactly at leg completion.
