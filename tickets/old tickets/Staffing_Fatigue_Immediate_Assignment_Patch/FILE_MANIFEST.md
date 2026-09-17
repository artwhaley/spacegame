# File Manifest and Ownership Guide

This is a planning manifest, not permission to overwrite files. Inspect the live tree first.

## New files expected

- `Assets/Scripts/ColonyPrototype/People/ColonistStatusComponent.cs`
- `Assets/Scripts/ColonyPrototype/People/ColonistStatusComponent.cs.meta`
- focused fatigue/duty tests under the appropriate test assemblies
- focused runtime lifecycle PlayMode tests
- `Assets/GameData/Classes/Doctor.asset` and `.meta`
- `Assets/GameData/Classes/Nurse.asset` and `.meta`
- `STAFFING_PATCH_BASELINE.md`
- `STAFFING_PATCH_ACCEPTANCE.md`

## Existing production files expected to change

- `Assets/Scripts/ColonyPrototype/People/ColonistAgent.cs`
- `Assets/Scripts/ColonyPrototype/People/HabitationComponent.cs`
- `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs`
- `Assets/Scripts/ColonyPrototype/People/StaffingComponent.cs`
- `Assets/Scripts/ColonyPrototype/People/FacilityPerformanceComponent.cs`
- `Assets/Scripts/ColonyPrototype/People/PopulationManager.cs`
- `Assets/Scripts/ColonyPrototype/Content/StaffingRoleDefinition.cs`
- `Assets/Scripts/ColonyPrototype/Core/SimulationManager.cs`
- every live `ISimulationTickable` implementation found by repository search
- `Assets/Scripts/ColonyPrototype/Logistics/LogisticsManager.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/TransportVehicleComponent.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/ShipComponent.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/ShipCrewDutyComponent.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/PassengerCarrierComponent.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/TransportExecutorComponent.cs`
- `Assets/Scripts/ColonyPrototype/Extraction/ExtractionMissionController.cs`
- `Assets/Scripts/ColonyPrototype/Logistics/ContractManager.cs`
- `Assets/Scripts/ColonyPrototype/Production/ResourceConverterComponent.cs`
- `Assets/Person.prefab`
- `Assets/SpaceSim.unity`
- authored role/habitation assets as required

## Existing tests expected to change

- `Assets/Tests/EditMode/StaffingTests.cs`
- `Assets/Tests/EditMode/StaffingManagerTests.cs`
- `Assets/Tests/EditMode/FacilityPerformanceTests.cs`
- `Assets/Tests/EditMode/StaffingTestHarness.cs`
- `Assets/Tests/EditMode/ResourceConverterTests.cs`
- relevant PlayMode test files/assemblies

The executor may add focused test files instead of growing one large test class.

## Documentation expected to change

- `ARCHITECTURE.md`
- `CONTENT_AUTHORING.md`
- `STAFFING_ARCHITECTURE.md`
- `STAFFING_AUTHORING.md`

## Files that must stay deleted/absent

- `RecipeStaffingRule.cs`
- `WorkScheduleComponent.cs`

Do not recreate an obsolete type merely to satisfy an old test or ticket.

## Cross-ticket ownership

```text
T00  test harness and baseline truth
T01  personal status, duty model, authoring modifiers
T02  employment mutation semantics
T03  activity/shift/fatigue/commute state machine
T04  facility provider liveness and converter pause/resume
T05  tickable/population/vehicle lifecycle
T06  assets, prefab, scene, documentation
T07  defects revealed by full acceptance only
T08  unified ship staffing assignments, shared-base validation, and physical-duty identity
T09  cockpit handover, safe release, movement ownership, and operation completion
T10  live content migration and multi-cycle ship-crew acceptance
```

If a defect spans tickets, fix it in the earliest ticket that owns the violated contract and add the regression test there.
