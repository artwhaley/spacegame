# S04 — Migrate callers, delete `ShipMovementComponent`, timed transfers, scene ports

Depends on S03. Read `01_LOCKED_DESIGN.md` §4 and §6.

## May modify
- `Assets/Scripts/ColonyPrototype/Vehicles/TransportExecutorComponent.cs`
- `Assets/Scripts/ColonyPrototype/Extraction/ExtractionMissionController.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/ShipCrewDutyComponent.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/ShipComponent.cs` (remove now-unused public mutators)
- `Assets/Scripts/ColonyPrototype/Vehicles/ShipMovementComponent.cs` — **delete** (and `.meta`)
- `Assets/Tests/EditMode/TransportExecutorTests.cs`, `ExtractionCompositionTests.cs`,
  `PilotDutyTests.cs`, `TransportCompositionTests.cs`, `StaffingTestHarness.cs` — swap
  the mover for a `ShipVoyageComponent` + high-thrust fixture profile and authored
  ports on fixture anchors
- `Assets/SpaceSim.unity`, `Assets/GameData/Flight/*`
- `ARCHITECTURE.md` (Ship composition + tick list), `CONTENT_AUTHORING.md` (new
  section: "Author docking ports and a flight profile")

## Steps
1. Executor: replace each travel branch with request-then-poll per §4. `Blocked` from
   the voyage → executor `Blocked` with the same reason. Remove all lease calls from the
   executor; the voyage owns the lease lifecycle.
2. Extraction: Loiter at deposit, Berth at unload. Keep retry-when-destination-blocked
   semantics by issuing the berth voyage only when the existing pressure check passes.
3. Crew return: `RequestVoyageToBerth(crewChangeBase, CrewReturn, 10)`; disembark on
   `IsDocked`.
4. Timed transfers: add `loadGameHoursPerUnit`, `loadGameHoursPerPassenger`,
   `TransferProgress01`; mutate inventories at phase end only.
5. Delete `ShipMovementComponent`; fix compile in tests via fixture profile.
6. Scene: ports/controls/holding slots per §6; `Shuttle.asset` / `MiningShip.asset`
   assigned; `gameHoursPerRealSecond = 0.0166667`; separate mesh roots from ship roots.
7. Docs.

## Forbidden
- Any change to `TransportContract`, `ContractManager`, `LogisticsManager`,
  `TransportVehicleComponent`, staffing, or employment.
- Reintroducing a direct `transform.position` write anywhere except `ShipVoyageComponent`.

## Acceptance
- [ ] `grep -rn "MoveToward\|ShipMovementComponent" Assets/` → no hits.
- [ ] `grep -rn "TryClaimMovement\|ReleaseMovement" Assets/Scripts` → only in
      `ShipVoyageComponent.cs` and `ShipCrewDutyComponent.cs` (stale-lease recovery).
- [ ] Observable, fresh Play Mode 24 game-hours at 1× then 10×: freight and passenger
      contracts complete; Water Processor staff arrive by shuttle; mining ship loiters
      at the asteroid and berths at CommandPod; with the second shuttle temporarily
      duplicated, one holds at CommandPod while the other occupies the single Farm
      port, then docks after release. Paste `ship.phase` history for one full voyage.
- [ ] No console errors; no ship ever reports `Blocked` in the authored scene.
