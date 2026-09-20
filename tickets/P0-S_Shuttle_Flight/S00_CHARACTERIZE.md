# S00 — Characterize ship movement before the voyage refactor

No source changes.

## Must create
- `tickets/P0-S_Shuttle_Flight/S00_CHARACTERIZATION.md`

## Steps
1. For `TransportExecutorComponent`, `ExtractionMissionController`, `ShipCrewDutyComponent`:
   list every call to `MoveToward`, `BeginUndocking`, `MarkDeparted`, `BeginDocking`,
   `TryCompleteArrival`, `TryClaimMovement`, `ReleaseMovement`, with file:line and the
   state the caller is in when it makes the call.
2. Draw each caller's state machine as it exists (states → transitions → movement calls).
3. List every `LocationAnchor` in `Assets/SpaceSim.unity` that ships travel to
   (initialDock, crewChangeBase, contract endpoints via stock-policy inventories,
   extraction unload location, deposit).
4. List every test that references `ShipMovementComponent` or `movementSpeed`.
5. Record `gameHoursPerRealSecond`, `tickIntervalSeconds`, and the real-time duration
   of one current Shuttle hop CommandPod→Farm at 1×.

## Acceptance
- [ ] Every movement call site is mapped to the post-refactor equivalent in
      `01_LOCKED_DESIGN.md` §4.
- [ ] `git status` shows only the new markdown.
