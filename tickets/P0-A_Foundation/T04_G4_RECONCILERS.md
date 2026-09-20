# T04 (G4) — Extract `PilotDutyReconciler` and `ColonistReconciler`

Depends on T03. Read `01_LOCKED_DESIGN.md` Part 1.

## Goal
Move per-colonist tick/reconcile logic and ship-crew reconcile logic into two plain
classes. `StaffingManager.SimulationTick` becomes pure orchestration. Behavior and
duty-phase writes unchanged. After this ticket `StaffingManager.cs` must be ≤ 250 lines.

## Must create
- `Assets/Scripts/ColonyPrototype/People/Staffing/PilotDutyReconciler.cs`
- `Assets/Scripts/ColonyPrototype/People/Staffing/ColonistReconciler.cs`

## May modify
- `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs`

## Design
```csharp
internal sealed class PilotDutyReconciler
{
    public PilotDutyReconciler(List<ColonistAgent> knownColonists, CommuteBatcher commutes, System.Action<string> log);
    public void TickResponsibleShipDuties(float deltaGameHours, float hour);
    public void Tick(ColonistAgent colonist, EmploymentAssignment employment, ColonistStatusComponent status, float deltaGameHours, float hour);
    public void Reconcile(ColonistAgent colonist, EmploymentAssignment employment, LocationAnchor home, float hour);
    public static ShipComponent FindReturningShip(ColonistAgent colonist);
    public static bool IsShipEmployment(EmploymentAssignment employment);
    public static ShipComponent ShipForEmployment(EmploymentAssignment employment);
    public static bool HasActiveShipOperation(ShipComponent ship);
}

internal sealed class ColonistReconciler
{
    public ColonistReconciler(PilotDutyReconciler pilots, CommuteBatcher commutes, System.Action<string> log);
    public void Reconcile(ColonistAgent colonist, float hour);       // was ReconcileColonist
    public void Tick(ColonistAgent colonist, float deltaGameHours, float hour);  // was TickColonist
    // private: RouteHomeOrRest, EndDutyIfActive, SetActivity, EnsureStatus, HomeRestfulness
}
```
Shared helpers `SetActivity`, `EnsureStatus`, `HomeRestfulness` are `internal static`
on `ColonistReconciler`; the pilot reconciler calls them.

## Steps
1. Move bodies verbatim. Where a moved method called a `StaffingManager` private, it
   now calls the moved equivalent.
2. `SimulationTick` becomes exactly the five-step order in the locked design.
3. `IsSafelyAtHome` stays on the facade and uses `CommuteBatcher.HasActivePassengerContract`.

## Forbidden
- Changing the order of `Reconcile` before `Tick` per colonist, or ship duties first.
- Any new or removed `SetDutyState` call.

## Acceptance
- [ ] `StaffingManager.cs` ≤ 250 lines; each new file ≤ 300.
- [ ] `grep -n SetDutyState` across `People/` shows writes only in the two reconcilers.
- [ ] Observable: run `SpaceSim.unity` for 48 game-hours at 10×; the `ReadinessHistory`
      JSONL contains the same sequence of `duty.*` event kinds for `Pilot 3` as a run
      made before this ticket (paste both sequences).
