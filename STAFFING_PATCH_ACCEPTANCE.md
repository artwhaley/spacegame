# Staffing Patch Acceptance

This file is completed by T07 after a licensed Unity run. It is intentionally
not marked accepted by source inspection alone.

## Automated results

| Pass | Result |
|---|---|
| Source assembly compile (`dotnet build` generated Unity projects) | Passed: Runtime, Tests, and PlayModeTests, 0 errors |
| Unity Editor import/compile | Editor.log later records Runtime/Editor/Tests Csc jobs with `ExitCode 0` after asset import |
| EditMode | Pending licensed Unity run |
| PlayMode | Pending licensed Unity run |

## T08–T10 implementation status

Ship crew now uses the same explicit `EmploymentAssignment` as every other
workplace: a normal ship `StaffingComponent` owns Pilot role/shift assignments,
while `ShipCrewDutyComponent.ResponsiblePilot` is the temporary physical lease
for the one pilot aboard. Employment persists across shift end and sleep; only
an eligible on-shift pilot at the fixed crew-change base boards. Fatigue accrues
while the lease is active at the role exertion multiplier. Shift/exhaustion,
reassignment, or invalid crew configuration blocks new dispatch, completes
accepted transport or extraction, returns the ship crew-only to base, and then
disembarks the pilot. Carrier eligibility uses the responsible lease, so an
assigned pilot may ride another ship while a ship cannot deadlock on its own
responsible pilot.

The source/runtime and test assemblies compile cleanly with the newly added
component included (0 errors). Licensed Unity EditMode/PlayMode totals and a
fresh scene smoke log still require an editor run; no pass is claimed here.

The generated Unity project assemblies were compiled after the patch with:

```text
dotnet build ColonyPrototype.Runtime.csproj --no-restore
dotnet build ColonyPrototype.Tests.csproj --no-restore
dotnet build ColonyPrototype.PlayModeTests.csproj --no-restore
```

All three completed successfully with zero warnings and zero errors. Unity
regenerated the project entries for the new source/test files; no hand-edited
compile entries are retained, and Unity remains the source of truth for
import/assembly generation.

The already-open editor initially logged a transient test-assembly dependency
cascade while it was importing the changed assets. It then completed three
successful Tundra builds; no later C# errors or runtime exception traces appear
in the available log. The batch-mode test invocation could not run concurrently
with that editor instance, so test totals remain pending.

## Runtime and adversarial observations

Pending the licensed 32-game-hour `SpaceSim.unity` smoke run and the explicit
reassignment, in-flight, provider-toggle, converter-toggle, and lifecycle checks
listed in `tickets/Staffing_Fatigue_Immediate_Assignment_Patch/09_T07_TRUE_ACCEPTANCE.md`.

A new batch-mode Unity test attempt was blocked because another Unity instance
already has this project open; the earlier baseline attempt was blocked by the
Unity licensing client before compilation. No Unity test totals are claimed.

## Excluded features confirmed

No staffing UI, automatic vacancy filler/call-in allocator, contract rerouting or
multi-stop transport, hunger/thirst/morale, or general overtime scheduling are
part of this patch. Pilot release may produce bounded completion time while an
accepted flight finishes.

## 24-hour clock patch execution status

The clock, daily shift migration, schedule dump, and focused regression tests
are implemented according to `tickets/24_Hour_Day_Clock_Patch/`. Runtime, EditMode,
and PlayMode generated assemblies compile cleanly when Unity's new source/test
files are included through a temporary MSBuild import. The checked-in generated
`.csproj` files were not hand-edited and still await the open Unity editor's
normal project refresh. Licensed Unity test totals and the 48-hour live-scene
gate remain pending.

The 24-hour clock patch is specified in
`tickets/24_Hour_Day_Clock_Patch/`. Its acceptance gate additionally requires
crossing two midnights, proving that Shift A does not restart at hour 16, and
capturing the all-colonist daily schedule dump. Those observations are pending
until the implementation and licensed Unity run complete.
