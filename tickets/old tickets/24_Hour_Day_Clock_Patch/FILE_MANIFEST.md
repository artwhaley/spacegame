# File Manifest

## Add

- `Assets/Scripts/ColonyPrototype/Core/SimulationTime.cs`
- `Assets/Scripts/ColonyPrototype/Core/SimulationTime.cs.meta`
- `Assets/Tests/EditMode/SimulationTimeTests.cs`
- `Assets/Tests/EditMode/SimulationTimeTests.cs.meta`

The execution agent may add one focused schedule-table test file instead of
placing those assertions in an existing staffing test file. If added, include
its `.meta` and keep it in the existing EditMode test assembly.

## Modify

- `Assets/Scripts/ColonyPrototype/Core/SimulationManager.cs`
- `Assets/Scripts/ColonyPrototype/Core/SimulationLog.cs`
- `Assets/Scripts/ColonyPrototype/Content/ShiftPatternDefinition.cs`
- `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs`
- `Assets/Tests/EditMode/StaffingTestHarness.cs`
- `Assets/Tests/EditMode/StaffingTests.cs`
- `Assets/Tests/EditMode/StaffingManagerTests.cs`
- `Assets/Tests/EditMode/FacilityPerformanceTests.cs`
- `Assets/Tests/EditMode/PilotDutyTests.cs`
- relevant existing PlayMode test file, or one new focused PlayMode file
- `Assets/SpaceSim.unity` only if Unity rewrites the preserved asset reference or
  the execution discovers stale serialized shift data
- `ARCHITECTURE.md`
- `CONTENT_AUTHORING.md`
- `STAFFING_ARCHITECTURE.md`
- `STAFFING_AUTHORING.md`
- `STAFFING_PATCH_ACCEPTANCE.md`

## Rename together, preserving GUID

- `Assets/GameData/Shifts/TwoShift8x8.asset`
  -> `Assets/GameData/Shifts/Daily8HourShifts.asset`
- `Assets/GameData/Shifts/TwoShift8x8.asset.meta`
  -> `Assets/GameData/Shifts/Daily8HourShifts.asset.meta`

## Must not modify for this patch

- resource recipes or converter architecture;
- contract routing/cancellation semantics;
- fatigue rates or thresholds;
- assignment capacity or automatic staffing behavior;
- old historical ticket packets, except to add a brief superseded pointer if an
  execution agent finds an active tool consuming them.
