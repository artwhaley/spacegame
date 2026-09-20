# T01 (G1) — Extract `ScheduleReportFormatter`

Depends on T00. Read `01_LOCKED_DESIGN.md` Part 1.

## Goal
Move all pure formatting/labeling helpers out of `StaffingManager` into
`internal static class ScheduleReportFormatter`. Zero behavior change; the string
output of `BuildDailyScheduleTable` must be byte-identical.

## Must create
- `Assets/Scripts/ColonyPrototype/People/Staffing/ScheduleReportFormatter.cs`
- `Assets/Tests/EditMode/ScheduleReportFormatterTests.cs` (one test: table for a
  fixture colonist with one assignment produces the expected header + row)

## May modify
- `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs`

## Steps
1. Move `BuildDailyScheduleTable` body into
   `ScheduleReportFormatter.BuildDailyScheduleTable(IReadOnlyList<ColonistAgent>, float absoluteGameHour)`.
   `StaffingManager.BuildDailyScheduleTable(float)` stays as a one-line forwarder.
2. Move `CompareColonistsForSchedule`, `FormatWorkWindow`, `FormatOffDutyWindow`,
   `ValidDailyShift`, `FormatDailyBoundary`, `FormatLastDuty`, `AnchorLabel`,
   `ColonistLabel`, `ShipLabel`, `RoleLabel` as `internal static`.
3. Anywhere else in `StaffingManager` that used a label helper now calls
   `ScheduleReportFormatter.X`.
4. Remove the moved methods from `StaffingManager`.

## Forbidden
- Changing any format string, ordering, or culture handling.
- Touching any method that mutates state.

## Acceptance
- [ ] `StaffingManager.cs` shrinks by the moved line count and compiles.
- [ ] `ScheduleReportFormatter.cs` ≤ 150 lines, no instance state, no Unity lifecycle.
- [ ] Observable: entering Play Mode in `SpaceSim.unity` and reading
      `StaffingManager.LastScheduleDump` after the first day boundary shows the same
      table as before the change (paste both in your report).
