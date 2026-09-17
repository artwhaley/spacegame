# T07 — True Acceptance

Depends on T06. This ticket adds no features. Fix defects and test gaps only.

## 1. Static audit

Run and inspect repository searches for:

```text
pendingEmployment
hasPendingEmploymentChange
AssignmentResult.Pending
RecipeStaffingRule
WorkScheduleComponent
FarmController
ISimulationTickable
requiredClass
additionalProviders
```

Expected:

- no pending-employment symbols;
- deleted legacy recipe-staffing/work-schedule types are not reintroduced;
- no Farm production controller is reintroduced;
- every tickable uses unified registration;
- every authored role has a class;
- additional providers remain supported dynamically.

Also verify staffing code has no compile-time dependency on Food, Water, Ice, `RecipeDefinition`, or `ResourceConverterComponent`.

## 2. Full automated suite

Run, in a clean editor process:

1. compile/import pass;
2. all EditMode tests;
3. all PlayMode tests.

No ignored test from T00 may remain. No unexpected error or exception log is allowed. Warnings must be individually understood; remove recurring warnings caused by this patch.

The suite must include and pass these behavior groups:

- class eligibility and multi-class colonists;
- explicit shifts and capacity;
- immediate atomic assignment/unassignment;
- invalid assignment rollback;
- duty history and end reasons;
- fatigue arithmetic, modifiers, threshold, and recovery latch;
- early departure and same-tick loss of production contribution;
- home/work/third-location routing;
- immutable current flight followed by new routing;
- no duplicate passenger contracts;
- dynamic provider add/disable/re-enable/destroy;
- converter mid-batch pause/resume without duplication;
- component-before-manager and manager-before-component lifecycle;
- disable/re-enable/destroy and manager replacement;
- deterministic tick priority;
- Doctor/Nurse class separation;
- automated facility with no staffing provider remains operational at default multiplier;
- Farm production remains resource-converter-driven.

Do not assert internal list layout, exact log prose, or passenger-contract history where a public state/result is available.

## 3. Playable-scene smoke run

Run `SpaceSim.unity` for at least 32 game hours at accelerated speed. Observe and record in `STAFFING_PATCH_ACCEPTANCE.md`:

- both authored workers commute only for their explicit shifts;
- fatigue rises only while Working;
- fatigue falls only while Sleeping;
- any worker reaching `0.90` leaves early and stops output immediately;
- recovered worker behavior follows the `0.20` latch;
- Farm Water input and Food output continue through resource conversion;
- shuttle and freight continue operating;
- converter disable/re-enable pauses and resumes work;
- provider disable/re-enable changes performance on the next evaluation;
- no repeated contracts, stranded third-location workers, missing references, or recurring Console errors.

If the default scene cannot naturally reach one boundary during 32 hours, use a temporary Inspector value or a PlayMode acceptance fixture to exercise it, restore the scene value, and document the method.

## 4. Manual adversarial checks

Perform these in PlayMode without editing contracts:

1. Reassign a Working farmer to another valid workplace/shift: old output loses them immediately and they request travel.
2. Reassign a passenger mid-flight: current flight completes; after landing they request the correct next trip.
3. Unassign a passenger mid-flight: current flight completes; after landing they request home.
4. Disable and re-enable the staffing provider: performance disappears and returns.
5. Disable and re-enable a converter mid-batch: progress pauses and resumes once.
6. Add an enabled provider at runtime: it affects the next evaluation.
7. Disable/re-enable a tickable: it stops and resumes once.

Record observed results, not only “passed.”

## 5. Final report

`STAFFING_PATCH_ACCEPTANCE.md` must contain:

- Unity version and exact commands;
- EditMode and PlayMode totals;
- smoke-run observations;
- adversarial-check observations;
- any warnings and their disposition;
- changed-file summary;
- explicit confirmation that excluded features were not implemented.

## Final pass condition

True acceptance requires every automated suite green, every manual check successful, no recurring runtime errors, and documentation matching actual behavior. A partially green suite or “game seems to run” is not acceptance.
