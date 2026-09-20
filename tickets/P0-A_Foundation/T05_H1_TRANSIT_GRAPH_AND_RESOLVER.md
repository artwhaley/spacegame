# T05 (H1) — `TransitLinkComponent`, `TransitGraph`, `RoutePlan`, `RouteResolver`

Independent of the G tickets. Read the historical Part 2 proposal in
`01_LOCKED_DESIGN.md`; confirm the API against the active window and current source.

## Goal
Add the walk-link registry and the pure route resolver. Nothing consumes them yet.

## Must create
- `Assets/Scripts/ColonyPrototype/World/Transit/TransitLinkComponent.cs`
- `Assets/Scripts/ColonyPrototype/World/Transit/TransitGraph.cs`
- `Assets/Scripts/ColonyPrototype/World/Transit/RoutePlan.cs`
- `Assets/Scripts/ColonyPrototype/World/Transit/RouteResolver.cs`
- `Assets/Tests/EditMode/RouteResolverTests.cs`

## May modify
- nothing else

## Tests to write (design artifacts; they define the contract)
1. Same anchor → `AlreadyThere`.
2. A–B linked → `Walk`, `estimatedHours == traversalHours`, path `[A, B]`.
3. A–B–C linked, A–C not → `Walk`, path `[A, B, C]`, hours summed.
4. Two paths, shortest by hours wins (not by hop count).
5. A–B link component disabled → no walk; with a `personnelEnabled` vehicle present
   → `Ship`; with none → `Unreachable / NoWalkPathAndNoPersonnelVehicle`.
6. `LogisticsManager.Instance == null` and no walk path → `Unreachable / LogisticsUnavailable`.
7. Link with `endpointA == endpointB` is never registered (`IsOpen == false`).

Use the fixture style in `Assets/Tests/EditMode/StaffingTestHarness.cs` for creating
anchors and managers.

## Forbidden
- Any reference to `ColonistAgent`, `StaffingManager`, or contracts. This layer knows
  anchors, links, and whether a personnel vehicle exists — nothing else.
- Allocation inside `TryFindWalkPath` beyond the caller's buffer and a reusable
  static scratch set/queue.

## Acceptance
- [ ] All four runtime files compile; total ≤ 350 lines.
- [ ] Observable: add a `TransitLinkComponent` between CommandPod and Farm in a scratch
      copy of the scene, enter Play Mode, and `TransitGraph.Links.Count == 1`; disable
      the component and the count drops to 0. (Do not commit the scene edit — T07 owns it.)
