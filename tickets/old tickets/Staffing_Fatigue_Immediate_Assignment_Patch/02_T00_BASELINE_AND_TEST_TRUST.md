# T00 — Baseline and Restore Test Trust

## Goal

Capture the current behavior and make the Unity test harness trustworthy before changing semantics. This ticket does not implement fatigue or immediate reassignment.

## Inspect

- `ProjectSettings/ProjectVersion.txt`
- all files under `Assets/Tests/EditMode` and `Assets/Tests/PlayMode` if present
- all singleton `Awake`/`OnEnable`/`Start` paths used by staffing tests
- the current Unity log produced by the last run, if it remains in the repository or known test-output location
- `StaffingTestHarness.cs`

## Required work

1. Record the exact Unity editor executable/version and reusable batch commands in `STAFFING_PATCH_BASELINE.md` at repository root.
2. Run a compile-only batch pass, the full EditMode suite, and the full PlayMode suite. Record totals and every failure category. Do not describe a red suite as a pass because a selected test passed.
3. Repair tests that assume EditMode automatically invokes scene lifecycle messages. Test setup must explicitly construct and wire the domain objects it needs through public APIs. Use PlayMode for behavior that specifically depends on Unity lifecycle messages.
4. Add a shared teardown utility that destroys created GameObjects and ScriptableObjects and leaves singleton/static state clean. Do not add test reset hooks to production classes unless a public runtime reset is itself a valid game operation.
5. Remove or rewrite assertions that encode superseded behavior:
   - pending assignment until home;
   - null required classes for Doctor/Nurse roles;
   - contract-history searches as proof of work;
   - cached providers remaining active after disable.
6. Do not delete a real failing test merely to make the suite green. If its intended behavior belongs to a later ticket, update its name/assertion to the locked behavior and mark it ignored with a reference to the exact ticket that will enable it. Remove all such ignores by T07.

## Required baseline observations

Document:

- whether the Farm produces through `ResourceConverterComponent`;
- whether staffing affects `FacilityPerformanceComponent` and therefore conversion rate;
- current assignment/reassignment behavior;
- current passenger-contract behavior during a flight;
- all `ISimulationTickable` implementations and their registration callbacks;
- all facility-performance providers;
- recurring warnings/errors in a 32-game-hour smoke run.

## Files allowed to change

- test assembly files and test helpers;
- `STAFFING_PATCH_BASELINE.md`;
- no gameplay production file unless a compile error proves the existing code is internally inconsistent. Record any such exception in the baseline.

## Acceptance gate

- The project compiles.
- The baseline document contains commands, results, and failure categories.
- Tests no longer depend on private lifecycle invocation or incidental singleton timing.
- Remaining ignored/failing tests each name a later ticket; there are no unexplained failures.
- No runtime behavior has intentionally changed.

