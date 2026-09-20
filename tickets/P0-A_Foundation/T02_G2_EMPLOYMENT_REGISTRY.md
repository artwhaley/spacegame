# T02 (G2) — Extract `EmploymentRegistry`

Depends on T01. Read `01_LOCKED_DESIGN.md` Part 1.

## Goal
Move the employment mutation and query API into a plain class owned by
`StaffingManager`. The facade keeps every public signature as a one-line forwarder.

## Must create
- `Assets/Scripts/ColonyPrototype/People/Staffing/EmploymentRegistry.cs`

## May modify
- `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs`
- `Assets/Tests/EditMode/StaffingManagerTests.cs` (only if a test reached into a
  private member; public API is unchanged so this should be rare)

## Design
```csharp
internal sealed class EmploymentRegistry
{
    public EmploymentRegistry(List<ColonistAgent> knownColonists, System.Action<string> log, System.Action<string> setDiagnostic);
    public AssignmentResult Assign(...same params as StaffingManager.Assign...);
    public AssignmentResult Unassign(ColonistAgent colonist);
    public AssignmentResult ValidateAssignment(...);
    public IReadOnlyList<ColonistAgent> GetEligibleColonists(StaffingComponent, StaffingRoleDefinition);
    public IReadOnlyList<AssignmentCandidate> GetAssignmentCandidates(...);
    public IReadOnlyList<ColonistAgent> GetAssignedWorkers(StaffingComponent, StaffingRoleDefinition, string shiftId);
    public void CollectAssignedWorkers(..., List<ColonistAgent> buffer);
    public int CountAssigned(StaffingComponent, StaffingRoleDefinition, string shiftId, ColonistAgent exclude);
    public void ValidateAuthoredEmployment();
    public int CountEmployed();
    public static string Describe(AssignmentResult result);
}
```
`ReleasePreviousEmployment` moves here as `private static`. The registry holds **no**
employment data; `ColonistAgent.currentEmployment` remains the authority.

## Steps
1. Create the class; move method bodies verbatim.
2. `StaffingManager.Awake` constructs it after `DiscoverColonists`, passing the same
   `knownColonists` list instance (not a copy).
3. Replace bodies in `StaffingManager` with forwarders. `UpdateCounts` uses
   `employment.CountEmployed()`.

## Forbidden
- Reordering validation checks inside `Assign`/`ValidateAssignment`.
- Changing `AssignmentResult`, `AssignmentCandidate`, or `EmploymentAssignment`.

## Acceptance
- [ ] Every existing test in `StaffingManagerTests.cs`, `StaffingTests.cs`,
      `PilotDutyTests.cs` compiles without edits to their assertions.
- [ ] `EmploymentRegistry.cs` ≤ 300 lines.
- [ ] Observable: in Play Mode, selecting `StaffingManager` and calling `Assign` for
      the scene's `Pilot 3` to the Shuttle roster yields the same `AssignmentResult`
      and `LastDiagnostic` text as before the change.
