# TEST HARNESS OVERHAUL REPORT

Starting SHA: `7bfc4364fc47c80c51800700537e55d40a0965bd`

Ending SHA: the commit containing this report.

## What changed

- Installed `TESTING_IN_EXPLORATION_MODE.md` as the canonical exploration-mode testing policy.
- Updated `agents.md` to require future agents to read that policy before adding/repairing tests.
- Updated active Architecture Constitution process policy and explicitly retired the historical "one EditMode test per ticket" quota.
- Added `docs/testing/BOB_AND_FRIENDS_ACCEPTANCE.md` and removed automated scene-YAML validation.
- Added `TEST_HARNESS_BASELINE.md`.
- Added Core / Regression / UnityIntegration NUnit categories to representative permanent suites.

## Deleted automated suites

- `BobAndFriendsFixtureTests` — parsed `.unity` YAML to validate manual fixture authoring.
- `ColonistOverheadDisplayTests` — presentation behavior with heavy private reflection; use human acceptance.
- `NoCodeAuthoringProofTests` — one reflected composition proof duplicated by owner-level converter coverage.
- `ColonistActivityRunnerInspectionTests` — one private/reflection-heavy inspection property test.
- `ReadinessRemediationTests` — historical remediation bundle rather than current owner-level contracts.
- `ColonistStatusTests` — legacy status-system coverage; canonical colonists use the newer composition.

Each deleted `.cs` file's `.meta` file was deleted with it.

## Pruned suites

- `ColonistBrainTests`: 39 -> 14 tests. Retained cooldown/commitment/work-exit and critical-hunger regressions.
- `MultiColonistIsolationTests`: 13 -> 6 tests. Retained per-colonist state, bed/reservation, workforce and identity isolation.
- `FoodContentionTests`: 6 -> 3 tests. Retained exclusive seat ownership, same-round scarce-offer protection, and committed-diner behavior.
- `ColonistStatsTests`: 40 -> 22 tests. Retained core physiology math/thresholds plus a small number of physical-activity rate invariants.

## Category installation

`Regression`: pruned brain/contention/isolation suites.

`Core`: DailyShiftWindow, OffDutyCompletionHistory, ResourceQuantityRules, SimulationTime, InventoryComponent, WorkforceManager, FacilityPerformance, UnifiedDispatch, FoodServiceAccess.

`UnityIntegration`: the two surviving PlayMode suites.

Categories are intentionally not a coverage bureaucracy. Unclassified surviving suites remain runnable in the full suite and can be categorized later when their permanence is clear.

## Size/result

- Test C# file count: 46 -> 40.
- The four largest pruned suites lost roughly 1,030 source lines combined.
- Deleted suites remove roughly another 57 KB of test source.
- Overall committed test-harness source is materially smaller (approximately 20% reduction from the inspected ~483 KB baseline).

## Validation limitation

This work was executed through GitHub write access. The Unity Editor/Test Runner is not available through that connector, so Unity compilation and automated test execution were **not** claimed.

Required human follow-up:

1. Pull the commit.
2. Open Unity and allow scripts to compile.
3. Run Core and Regression categories.
4. Run UnityIntegration if convenient.
5. Open `Assets/bobandfriends.unity` and perform the short smoke checklist in `docs/testing/BOB_AND_FRIENDS_ACCEPTANCE.md`.

If a surviving test fails, classify it before touching production code: real regression, stale assertion, implementation coupling, or environment problem.