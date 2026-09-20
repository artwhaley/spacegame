# T07 (H3) — Route resolution in commutes + scene corridor content

Depends on T04 and T06. Read `01_LOCKED_DESIGN.md` Part 2, sections "Changes to
existing files" and "Scene content".

## Goal
Every commute request is resolved: walk if a corridor path exists, shuttle otherwise,
Blocked with a reason if neither. The scene demonstrates both modes side by side.

## Must create
- `Assets/Tests/EditMode/CommuteRoutingTests.cs`

## May modify
- `Assets/Scripts/ColonyPrototype/People/Staffing/CommuteBatcher.cs`
- `Assets/Scripts/ColonyPrototype/People/Staffing/ColonistReconciler.cs`
- `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs` (only `IsSafelyAtHome`)
- `Assets/SpaceSim.unity`
- `STAFFING_ARCHITECTURE.md`, `CONTENT_AUTHORING.md` (add the corridor section)

## Steps
1. `CommuteBatcher.QueueCommute` gains the `out RouteBlockReason` and returns
   `RouteMode` per the locked design. Walk → `colonist.Pedestrian.BeginWalk(buffer)`.
   Ship → existing grouping. Keep a single reusable `List<LocationAnchor>` buffer.
2. `ColonistReconciler.Reconcile`: add the walking-in-transit early return (mirror the
   passenger branch), the Blocked-pedestrian branch, and the Unreachable → Blocked
   branch. On Walk, set activity `Walking`. Never request a commute for a colonist who
   is walking or holds an active passenger contract.
3. `IsSafelyAtHome` requires `!Pedestrian.IsWalking`.
4. Scene: add `Transit Links/Corridor CommandPod-Farm` (0.25 h). Leave Water Processor
   shuttle-only. Verify `Farm` staff `home` is CommandPod.
5. Docs: one section each — "Corridors vs shuttle service" (architecture), "Author a
   corridor" (authoring). Two paragraphs max each.

## Tests to write
1. Farm worker at CommandPod with A–Farm link at shift start → activity `Walking`,
   **no** passenger contract created.
2. Same with link disabled and a personnel shuttle in the fleet → passenger contract
   created, activity `WaitingForTransport` (unchanged behavior).
3. Same with link disabled and no personnel vehicle → `ColonistDutyState.Blocked`,
   `DutyBlocker` non-empty, no contract, no repeat requests across 5 ticks.
4. Link disabled mid-walk → next reconcile yields Blocked, then re-enabling the link
   yields `Walking` again from the committed waypoint.
5. Shift end at Farm → walks home; fatigue does not change while `Walking`.

## Forbidden
- Any change to `ContractManager`, `LogisticsManager`, dispatch, or ship code.
- A fallback that uses a shuttle when a walk path exists, or walks when none exists.
- Presentation.

## Acceptance
- [ ] Observable, 24 game-hours at 10× in `SpaceSim.unity`: Farm workers show
      `Walking` and produce `transit.walk.*` history with zero passenger contracts
      between CommandPod and Farm; Water Processor staff still produce passenger
      contracts. Paste the contract list and a history excerpt.
- [ ] Disabling the corridor in Play Mode flips Farm commutes to shuttle within one
      shift; deleting the Shuttle as well produces `Blocked` colonists with a readable
      `DutyBlocker`. No exceptions in the console.
