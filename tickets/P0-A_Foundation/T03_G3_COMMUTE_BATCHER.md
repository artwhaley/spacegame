# T03 (G3) — Extract `CommuteBatcher`

Depends on T02. Read `01_LOCKED_DESIGN.md` Part 1 (and skim Part 2 so the shape you
create can absorb T07 without a second refactor).

## Goal
Move commute grouping and passenger-contract creation into a plain class. Ship
behavior unchanged. In this ticket the batcher still only knows about ships; T07 adds
the resolver.

## Must create
- `Assets/Scripts/ColonyPrototype/People/Staffing/CommuteBatcher.cs`

## May modify
- `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs`

## Design
```csharp
internal sealed class CommuteBatcher
{
    public CommuteBatcher(System.Action<string> log);
    public void QueueCommute(LocationAnchor source, LocationAnchor destination, int priority, ColonistAgent colonist);
    public void Flush();
    public static bool HasActivePassengerContract(ColonistAgent colonist);
    public static TransportContract FindPassengerContract(ColonistAgent colonist);
    public static bool HasPassengerInTransit(ColonistAgent colonist);
    private sealed class CollectiveCommute { ... }   // moved verbatim
    private static int CommuteChunkSize();            // moved verbatim
}
```

## Steps
1. Move bodies verbatim. `commuteGroups` list becomes a private field of the batcher.
2. `StaffingManager` constructs one instance in `Awake`; `SimulationTick` calls
   `commutes.Flush()` at the same point `FlushCommutes()` was called.
3. Internal callers (`RouteHomeOrRest`, `ReconcileColonist`, `IsSafelyAtHome`) call the
   batcher/static helpers.

## Forbidden
- Changing chunk size logic, grouping keys, or priority clamping.
- Adding the resolver — that is T07.

## Acceptance
- [ ] `StaffingManager.cs` no longer contains `CollectiveCommute`.
- [ ] Observable: at shift A start in `SpaceSim.unity`, the same number of passenger
      contracts with the same source/destination/priority are created as before
      (read `ContractManager.Contracts` in the Inspector; paste before/after).
